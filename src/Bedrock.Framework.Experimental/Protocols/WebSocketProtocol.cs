using System;
using System.Net.Connections;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Protocols
{
    public class WebSocketProtocol
    {
        public WebSocketProtocol(WebSocket websocket, Connection connection)
        {
            WebSocket = websocket;
            Connection = connection;
        }

        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        private WebSocket WebSocket { get; }
        public Connection Connection { get; }

        public ValueTask<ValueWebSocketReceiveResult> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return WebSocket.ReceiveAsync(buffer, cancellationToken);
        }

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType webSocketMessageType, bool endOfMessage, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await WebSocket.SendAsync(buffer, webSocketMessageType, endOfMessage, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static WebSocketProtocol CreateFromConnection(Connection connection, bool isServer, string subProtocol, TimeSpan keepAliveInterval)
        {
            var websocket = WebSocket.CreateFromStream(connection.Stream, isServer, subProtocol, keepAliveInterval);
            return new WebSocketProtocol(websocket, connection);
        }
    }
}
