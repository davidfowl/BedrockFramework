using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework;

public class Server
{
    private readonly ServerBuilder _builder;
    private readonly ILogger<Server> _logger;
    private readonly List<RunningListener> _listeners = [];
    private readonly TaskCompletionSource<object> _shutdownTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly PeriodicTimer _timer;
    private Task _timerTask = Task.CompletedTask;

    internal Server(ServerBuilder builder)
    {
        _logger = builder.ApplicationServices.GetLoggerFactory().CreateLogger<Server>();
        _builder = builder;
        _timer = new PeriodicTimer(_builder.HeartBeatInterval);
    }

    public IEnumerable<EndPoint> EndPoints
    {
        get
        {
            foreach (var listener in _listeners)
            {
                yield return listener.Listener.EndPoint;
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var binding in _builder.Bindings)
            {
                await foreach (var listener in binding.BindAsync(cancellationToken).ConfigureAwait(false))
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

        _timerTask = StartTimerAsync();
    }

    private async Task StartTimerAsync()
    {
        using (_timer)
        {
            while (await _timer.WaitForNextTickAsync())
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

        for (int i = 0; i < _listeners.Count; i++)
        {
            tasks[i] = _listeners[i].Listener.UnbindAsync(cancellationToken).AsTask();
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

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

        if (_timer != null)
        {
            _timer.Dispose();

            await _timerTask.ConfigureAwait(false);
        }
    }

    private class RunningListener(Server server, ServerBinding binding, IConnectionListener listener)
    {
        private readonly ConcurrentDictionary<long, (ServerConnection Connection, Task ExecutionTask)> _connections = [];

        public void Start()
        {
            ExecutionTask = RunListenerAsync();
        }

        public IConnectionListener Listener { get; } = listener;
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
            var connectionDelegate = binding.Application;
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
                catch (ConnectionResetException)
                {
                    // Don't let connection aborted exceptions out
                }
                catch (ConnectionAbortedException)
                {
                    // Don't let connection aborted exceptions out
                }
                catch (Exception ex)
                {
                    server._logger.LogError(ex, "Unexpected exception from connection {ConnectionId}", connection.ConnectionId);
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
                    var connection = await listener.AcceptAsync().ConfigureAwait(false);

                    if (connection == null)
                    {
                        // Null means we don't have anymore connections
                        break;
                    }

                    var serverConnection = new ServerConnection(id, connection, server._logger);

                    _connections[id] = (serverConnection, ExecuteConnectionAsync(serverConnection));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    server._logger.LogCritical(ex, "Stopped accepting connections on {endpoint}", listener.EndPoint);
                    break;
                }

                id++;
            }

            // Don't shut down connections until entire server is shutting down
            await server._shutdownTcs.Task.ConfigureAwait(false);

            // Give connections a chance to close gracefully
            var tasks = new List<Task>(_connections.Count);

            foreach (var pair in _connections)
            {
                pair.Value.Connection.RequestClose();
                tasks.Add(pair.Value.ExecutionTask);
            }

            if (!await Task.WhenAll(tasks).TimeoutAfter(server._builder.ShutdownTimeout).ConfigureAwait(false))
            {
                // Abort all connections still in flight
                foreach (var pair in _connections)
                {
                    pair.Value.Connection.TransportConnection.Abort();
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            await listener.DisposeAsync().ConfigureAwait(false);
        }


        private IDisposable BeginConnectionScope(ServerConnection connection)
        {
            if (server._logger.IsEnabled(LogLevel.Critical))
            {
                return server._logger.BeginScope(connection);
            }

            return null;
        }
    }
}
