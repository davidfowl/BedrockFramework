using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    public class Server
    {
        private readonly ServerBuilder _builder;
        private readonly ILogger<Server> _logger;
        private readonly List<RunningListener> _listeners = new List<RunningListener>();
        private readonly TaskCompletionSource<object> _shutdownTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TimerAwaitable _timerAwaitable;
        private readonly CancellationTokenSource _stopAcceptingCts = new CancellationTokenSource();
        private Task _timerTask = Task.CompletedTask;

        internal Server(ServerBuilder builder)
        {
            _logger = builder.ApplicationServices.GetLoggerFactory().CreateLogger<Server>();
            _builder = builder;
            _timerAwaitable = new TimerAwaitable(_builder.HeartBeatInterval, _builder.HeartBeatInterval);
        }

        public IEnumerable<EndPoint> EndPoints
        {
            get
            {
                foreach (var listener in _listeners)
                {
                    yield return listener.Listener.LocalEndPoint;
                }
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                foreach (var binding in _builder.Bindings)
                {
                    await foreach (var listener in binding.ListenAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var runningListener = new RunningListener(this, binding, listener);
                        _listeners.Add(runningListener);
                        runningListener.Start();
                    }
                }
            }
            catch
            {
                await StopAsync().ConfigureAwait(false);

                throw;
            }

            _timerAwaitable.Start();
            _timerTask = StartTimerAsync();
        }

        private async Task StartTimerAsync()
        {
            using (_timerAwaitable)
            {
                while (await _timerAwaitable)
                {
                    foreach (var listener in _listeners)
                    {
                        listener.TickHeartbeat();
                    }
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            var tasks = new Task[_listeners.Count];

            //for (int i = 0; i < _listeners.Count; i++)
            //{
            //    tasks[i] = _listeners[i].Listener.UnbindAsync(cancellationToken).AsTask();
            //}

            _stopAcceptingCts.Cancel();

            // await Task.WhenAll(tasks).ConfigureAwait(false);

            // Signal to all of the listeners that it's time to start the shutdown process
            // We call this after unbind so that we're not touching the listener anymore (each loop will dispose the listener)
            _shutdownTcs.TrySetResult(null);

            for (int i = 0; i < _listeners.Count; i++)
            {
                tasks[i] = _listeners[i].ExecutionTask;
            }

            var shutdownTask = Task.WhenAll(tasks);

            if (cancellationToken.CanBeCanceled)
            {
                await shutdownTask.WithCancellation(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await shutdownTask.ConfigureAwait(false);
            }

            if (_timerAwaitable != null)
            {
                _timerAwaitable.Stop();

                await _timerTask.ConfigureAwait(false);
            }
        }

        private class RunningListener
        {
            private readonly Server _server;
            private readonly ServerBinding _binding;
            private readonly ConcurrentDictionary<long, (ServerConnection Connection, Task ExecutionTask)> _connections = new ConcurrentDictionary<long, (ServerConnection, Task)>();

            public RunningListener(Server server, ServerBinding binding, ConnectionListener listener)
            {
                _server = server;
                _binding = binding;
                Listener = listener;
            }

            public void Start()
            {
                ExecutionTask = RunListenerAsync();
            }

            public ConnectionListener Listener { get; }
            public Task ExecutionTask { get; private set; }

            public void TickHeartbeat()
            {
                foreach (var pair in _connections)
                {
                    pair.Value.Connection.TickHeartbeat();
                }
            }

            private async Task RunListenerAsync()
            {
                var connectionDelegate = _binding.Application;
                var listener = Listener;

                async Task ExecuteConnectionAsync(ServerConnection serverConnection)
                {
                    await Task.Yield();

                    var connection = serverConnection.TransportConnection;

                    try
                    {
                        using var scope = BeginConnectionScope(serverConnection);

                        await connectionDelegate(connection).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {

                    }
                    catch (OperationCanceledException)
                    {

                    }
                    //catch (ConnectionResetException)
                    //{
                    //    // Don't let connection aborted exceptions out
                    //}
                    //catch (ConnectionAbortedException)
                    //{
                    //    // Don't let connection aborted exceptions out
                    //}
                    catch (Exception ex)
                    {
                        _server._logger.LogError(ex, "Unexpected exception from connection {ConnectionId}", connection.LocalEndPoint.ToString());
                    }
                    finally
                    {
                        // Fire the OnCompleted callbacks
                        await serverConnection.FireOnCompletedAsync().ConfigureAwait(false);

                        await connection.DisposeAsync().ConfigureAwait(false);

                        // Remove the connection from tracking
                        _connections.TryRemove(serverConnection.Id, out _);
                    }
                }

                long id = 0;

                while (true)
                {
                    try
                    {
                        var connection = await listener.AcceptAsync(cancellationToken: _server._stopAcceptingCts.Token).ConfigureAwait(false);

                        if (connection == null)
                        {
                            // Null means we don't have anymore connections
                            break;
                        }

                        var serverConnection = new ServerConnection(id, connection, _server._logger);

                        _connections[id] = (serverConnection, ExecuteConnectionAsync(serverConnection));
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _server._logger.LogCritical(ex, "Stopped accepting connections on {endpoint}", listener.LocalEndPoint);
                        break;
                    }

                    id++;
                }

                // Don't shut down connections until entire server is shutting down
                await _server._shutdownTcs.Task.ConfigureAwait(false);

                // Give connections a chance to close gracefully
                var tasks = new List<Task>(_connections.Count);

                foreach (var pair in _connections)
                {
                    pair.Value.Connection.RequestClose();
                    tasks.Add(pair.Value.ExecutionTask);
                }

                if (!await Task.WhenAll(tasks).TimeoutAfter(_server._builder.ShutdownTimeout).ConfigureAwait(false))
                {
                    // Abort all connections still in flight
                    foreach (var pair in _connections)
                    {
                        _ = pair.Value.Connection.TransportConnection.CloseAsync(ConnectionCloseMethod.Immediate);
                    }

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }

                await listener.DisposeAsync().ConfigureAwait(false);
            }


            private IDisposable BeginConnectionScope(ServerConnection connection)
            {
                if (_server._logger.IsEnabled(LogLevel.Critical))
                {
                    return _server._logger.BeginScope(connection);
                }

                return null;
            }
        }
    }
}
