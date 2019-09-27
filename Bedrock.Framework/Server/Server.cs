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

                _listeners.Add((listener, RunListenerAsync(binding.EndPoint, listener, binding.ServerApplication)));
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

            for (int i = 0; i < _listeners.Count; i++)
            {
                tasks[i] = _listeners[i].ExecutionTask;
            }

            await Task.WhenAll(tasks);
        }

        private async Task RunListenerAsync(EndPoint endpoint, IConnectionListener listener, ConnectionDelegate connectionDelegate)
        {
            var connections = new ConcurrentDictionary<string, (ConnectionContext Connection, Task ExecutionTask)>();

            async Task ExecuteConnectionAsync(ConnectionContext connection)
            {
                await Task.Yield();

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
                    await connection.DisposeAsync();

                    // Remove the connection from tracking
                    connections.TryRemove(connection.ConnectionId, out _);
                }
            }

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

                    connections[connection.ConnectionId] = (connection, ExecuteConnectionAsync(connection));
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
            }

            // TODO: Give connections a chance to close gracefully

            var tasks = new List<Task>(connections.Count);

            // Abort all connections still in flight
            foreach (var pair in connections)
            {
                pair.Value.Connection.Abort();
                tasks.Add(pair.Value.ExecutionTask);
            }

            await Task.WhenAll(tasks);

            await listener.DisposeAsync();
        }
    }
}
