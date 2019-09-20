using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    internal class AzureSignalRConnectionListener : AzureSignalRDispatcher, IConnectionListener
    {
        private Channel<ConnectionContext> _acceptQueue = Channel.CreateUnbounded<ConnectionContext>();
        private ConcurrentDictionary<string, AzureSignalRConnectionContext> _connections = new ConcurrentDictionary<string, AzureSignalRConnectionContext>();

        public AzureSignalRConnectionListener(Uri uri, string token, ILoggerFactory loggerFactory)
            : base(uri, token, loggerFactory)
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
            foreach (var pair in _connections)
            {
                pair.Value.Disconnect();
            }

            _acceptQueue.Writer.TryComplete();
            return Task.CompletedTask;
        }

        protected override Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage)
        {
            var connection = new AzureSignalRConnectionContext(openConnectionMessage, this);
            _connections[openConnectionMessage.ConnectionId] = connection;
            _ = ProcessHandshakeAsync(connection);

            return Task.CompletedTask;
        }

        private async Task ProcessHandshakeAsync(AzureSignalRConnectionContext connection)
        {
            if (await connection.ProcessHandshakeAsync())
            {
                // The connection is accepted
                _acceptQueue.Writer.TryWrite(connection);

                // Start processing messages from the application
                connection.Start();
            }
        }

        protected override Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            if (_connections.TryRemove(closeConnectionMessage.ConnectionId, out var connection))
            {
                connection.Disconnect();
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
                catch (Exception)
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
    }
}