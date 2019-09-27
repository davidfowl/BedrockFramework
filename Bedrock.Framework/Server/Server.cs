using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    public class Server
    {
        private readonly ServerOptions _serverOptions;
        private readonly ILogger<Server> _logger;
        private readonly List<(IConnectionListener Listener, Task ExecutionTask)> _listeners = new List<(IConnectionListener Listener, Task ExecutionTask)>();
        private readonly TaskCompletionSource<object> _shutdownTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Server(ILoggerFactory loggerFactory, ServerOptions options)
        {
            _logger = loggerFactory.CreateLogger<Server>();
            _serverOptions = options ?? new ServerOptions();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            foreach (var binding in _serverOptions.Bindings)
            {
                var listener = await binding.ConnectionListenerFactory.BindAsync(binding.EndPoint, cancellationToken);
                _logger.LogInformation("Listening on {address}", binding.EndPoint);
                binding.EndPoint = listener.EndPoint;

                _listeners.Add((listener, RunListenerAsync(binding.EndPoint, listener, binding.Application)));
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            var tasks = new Task[_listeners.Count];

            for (int i = 0; i < _listeners.Count; i++)
            {
                tasks[i] = _listeners[i].Listener.UnbindAsync(cancellationToken).AsTask();
            }

            await Task.WhenAll(tasks);

            // Signal to all of the listeners that it's time to start the shutdown process
            // We call this after unbind so that we're not touching the listener anymore (each loop will dispose the listener)
            _shutdownTcs.TrySetResult(null);

            for (int i = 0; i < _listeners.Count; i++)
            {
                tasks[i] = _listeners[i].ExecutionTask;
            }

            await Task.WhenAll(tasks);
        }

        private async Task RunListenerAsync(EndPoint endpoint, IConnectionListener listener, ConnectionDelegate connectionDelegate)
        {
            var connections = new ConcurrentDictionary<long, (ServerConnection Connection, Task ExecutionTask)>();

            async Task ExecuteConnectionAsync(ServerConnection serverConnection)
            {
                await Task.Yield();

                var connection = serverConnection.TransportConnection;

                try
                {
                    await connectionDelegate(connection);
                }
                catch (ConnectionAbortedException)
                {
                    // Don't let connection aborted exceptions out
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected exception from connection {ConnectionId}", connection.ConnectionId);
                }
                finally
                {
                    // Fire the OnCompleted callbacks
                    await serverConnection.FireOnCompletedAsync();

                    await connection.DisposeAsync();

                    // Remove the connection from tracking
                    connections.TryRemove(serverConnection.Id, out _);
                }
            }

            long id = 0;

            while (true)
            {
                try
                {
                    var connection = await listener.AcceptAsync();

                    if (connection == null)
                    {
                        // Null means we don't have anymore connections
                        break;
                    }

                    var serverConnection = new ServerConnection(id, connection, _logger);

                    connections[id] = (serverConnection, ExecuteConnectionAsync(serverConnection));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Stopped accepting connections on {endpoint}", endpoint);
                    break;
                }

                id++;
            }

            // Don't shut down connections until entire server is shutting down
            await _shutdownTcs.Task;

            // Give connections a chance to close gracefully
            var tasks = new List<Task>(connections.Count);

            foreach (var pair in connections)
            {
                pair.Value.Connection.RequestClose();
                tasks.Add(pair.Value.ExecutionTask);
            }

            if (!await Task.WhenAll(tasks).TimeoutAfter(_serverOptions.GracefulShutdownTimeout))
            {
                // Abort all connections still in flight
                foreach (var pair in connections)
                {
                    pair.Value.Connection.TransportConnection.Abort();
                }

                await Task.WhenAll(tasks);
            }

            await listener.DisposeAsync();
        }
    }
}
