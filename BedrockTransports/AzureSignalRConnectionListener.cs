using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BedrockTransports
{
    internal class AzureSignalRConnectionListener : AzureSignalRDispatcher, IConnectionListener
    {
        private Channel<ConnectionContext> _acceptQueue = Channel.CreateUnbounded<ConnectionContext>();
        private ConcurrentDictionary<string, AzureSignalRConnectionContext> _connections = new ConcurrentDictionary<string, AzureSignalRConnectionContext>();

        public AzureSignalRConnectionListener(Uri uri, string token, ILoggerFactory loggerFactory)
            : base(uri, token, NullLoggerFactory.Instance)
        {
        }

        public async ValueTask<ConnectionContext> AcceptAsync(CancellationToken cancellationToken = default)
        {
            while (await _acceptQueue.Reader.WaitToReadAsync(cancellationToken))
            {
                if (_acceptQueue.Reader.TryRead(out var connection))
                {
                    return connection;
                }
            }
            return null;
        }

        public async ValueTask DisposeAsync()
        {
            await UnbindAsync();
        }

        public async ValueTask UnbindAsync(CancellationToken cancellationToken = default)
        {
            await StopAsync();
        }

        protected override Task CleanupConnectionsAsync()
        {
            _acceptQueue.Writer.TryComplete();
            return Task.CompletedTask;
        }

        protected override Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage)
        {
            var connection = new AzureSignalRConnectionContext(openConnectionMessage, OnDisposeAsync);
            _connections[openConnectionMessage.ConnectionId] = connection;
            _ = ProcessOutgoingMessagesAsync(connection);
            _acceptQueue.Writer.TryWrite(connection);

            return Task.CompletedTask;
        }

        private ValueTask OnDisposeAsync(ConnectionContext connectionContext)
        {
            // TODO: Send disconnect message
            return default;
        }

        protected override Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            if (_connections.TryRemove(closeConnectionMessage.ConnectionId, out var connection))
            {
                // TODO: fix this
                connection.Application.Output.Complete();
            }
            return Task.CompletedTask;
        }

        protected override async Task OnMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            if (_connections.TryGetValue(connectionDataMessage.ConnectionId, out var connection))
            {
                try
                {
                    var payload = connectionDataMessage.Payload;
                    // Log.WriteMessageToApplication(Logger, payload.Length, connectionDataMessage.ConnectionId);

                    if (payload.IsSingleSegment)
                    {
                        // Write the raw connection payload to the pipe let the upstream handle it
                        await connection.Application.Output.WriteAsync(payload.First);
                    }
                    else
                    {
                        var position = payload.Start;
                        while (connectionDataMessage.Payload.TryGet(ref position, out var memory))
                        {
                            await connection.Application.Output.WriteAsync(memory);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log.FailToWriteMessageToApplication(Logger, connectionDataMessage.ConnectionId, ex);
                }
            }
            else
            {
                // Unexpected error
                // Log.ReceivedMessageForNonExistentConnection(Logger, connectionDataMessage.ConnectionId);
            }
        }

        private async Task ProcessOutgoingMessagesAsync(AzureSignalRConnectionContext connection)
        {
            try
            {
                while (true)
                {
                    var result = await connection.Application.Input.ReadAsync();
                    if (result.IsCanceled)
                    {
                        break;
                    }

                    var buffer = result.Buffer;
                    if (!buffer.IsEmpty)
                    {
                        try
                        {
                            // Forward the message to the service
                            await WriteAsync(new ConnectionDataMessage(connection.ConnectionId, buffer));
                        }
                        catch (Exception ex)
                        {
                            // Log.ErrorSendingMessage(Logger, ex);
                        }
                    }

                    if (result.IsCompleted)
                    {
                        // This connection ended (the application itself shut down) we should remove it from the list of connections
                        break;
                    }

                    connection.Application.Input.AdvanceTo(buffer.End);
                }
            }
            catch (Exception ex)
            {
                // The exception means application fail to process input anymore
                // Cancel any pending flush so that we can quit and perform disconnect
                // Here is abort close and WaitOnApplicationTask will send close message to notify client to disconnect
                // Log.SendLoopStopped(Logger, connection.ConnectionId, ex);
                connection.Application.Output.CancelPendingFlush();
            }
        }

    }
}