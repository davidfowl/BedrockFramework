using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bedrock.Framework.Infrastructure;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class WebSocketProtocol
    {
        public WebSocketProtocol(WebSocket websocket, ConnectionContext connection)
        {
            WebSocket = websocket;
            Connection = connection;
        }

        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        private WebSocket WebSocket { get; }
        public ConnectionContext Connection { get; }

        public ValueTask<ValueWebSocketReceiveResult> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return WebSocket.ReceiveAsync(buffer, cancellationToken);
        }

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType webSocketMessageType, bool endOfMessage, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                await WebSocket.SendAsync(buffer, webSocketMessageType, endOfMessage, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static WebSocketProtocol CreateFromConnection(ConnectionContext connection, bool isServer, string subProtocol, TimeSpan keepAliveInterval)
        {
            var websocket = WebSocket.CreateFromStream(new DuplexPipeStream(connection.Transport.Input, connection.Transport.Output), isServer, subProtocol, keepAliveInterval);
            return new WebSocketProtocol(websocket, connection);
        }
    }
}
