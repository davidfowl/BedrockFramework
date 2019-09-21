using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bedrock.Framework
{
    public class ConnectionListenerHostedService : BackgroundService
    {
        private readonly ServerApplicationOptions _serverOptions;
        private readonly ILogger<ConnectionListenerHostedService> _logger;

        public ConnectionListenerHostedService(ILogger<ConnectionListenerHostedService> logger, IOptions<ServerApplicationOptions> options)
        {
            _logger = logger;
            _serverOptions = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tasks = new List<Task>(_serverOptions.Bindings.Count);
            foreach (var binding in _serverOptions.Bindings)
            {
                var listener = await binding.ConnectionListenerFactory.BindAsync(binding.EndPoint);
                _logger.LogInformation("Listening on {address}", binding.EndPoint);

                tasks.Add(RunListenerAsync(listener, binding.ServerApplication, stoppingToken));
            }

            await Task.WhenAll(tasks);
        }

        public async Task RunListenerAsync(IConnectionListener listener, ConnectionDelegate connectionDelegate, CancellationToken cancellationToken = default)
        {
            var unbindTask = new TaskCompletionSource<object>();
            var connections = new ConcurrentDictionary<string, (ConnectionContext Connection, Task ExecutionTask)>();

            cancellationToken.Register(async () =>
            {
                try
                {
                    await listener.UnbindAsync();
                }
                finally
                {
                    unbindTask.TrySetResult(null);
                }
            });

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
                    var connection = await listener.AcceptAsync(cancellationToken);

                    if (connection == null)
                    {
                        // Null means we don't have anymore connections
                        break;
                    }

                    connections[connection.ConnectionId] = (connection, ExecuteConnectionAsync(connection));
                }
                catch (OperationCanceledException)
                {
                    // The accept loop was cancelled
                    break;
                }
            }

            // Wait for the listener to close, no new connections will be established
            await unbindTask.Task;

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
