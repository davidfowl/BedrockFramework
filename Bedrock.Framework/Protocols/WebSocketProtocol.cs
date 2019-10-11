using System;
using System.Net.WebSockets;
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

        public WebSocket WebSocket { get; }
        public ConnectionContext Connection { get; }

        public static WebSocketProtocol CreateFromConnection(ConnectionContext connection, bool isServer, string subProtocol, TimeSpan keepAliveInterval)
        {
            var websocket = WebSocket.CreateFromStream(new DuplexPipeStream(connection.Transport.Input, connection.Transport.Output), isServer, subProtocol, keepAliveInterval);
            return new WebSocketProtocol(websocket, connection);
        }
    }
}
