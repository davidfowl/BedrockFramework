using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Connections;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public class SocketsTransport
    {
        public SocketsTransport()
        {
            ConnectionListenerFactory = new SocketsConnectionListenerFactory(this);
            ConnectionFactory = new SocketsConnectionFactory(this);
        }

        public ConnectionListenerFactory ConnectionListenerFactory { get; }
        public ConnectionFactory ConnectionFactory { get; }

        internal async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(endPoint);
            var ns = new NetworkStream(socket);
            return Connection.FromStream(ns);
        }

        internal ValueTask<ConnectionListener> ListenAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken)
        {
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(endPoint);
            socket.Listen();
            return ValueTask.FromResult<ConnectionListener>(new SocketsConnectionListener(socket, options));
        }

        private class SocketsConnectionListener : ConnectionListener
        {
            private readonly Socket _socket;
            public SocketsConnectionListener(Socket socket, IConnectionProperties options)
            {
                _socket = socket;
                ListenerProperties = options;
            }

            public override IConnectionProperties ListenerProperties { get; }

            public override EndPoint LocalEndPoint => _socket.LocalEndPoint;

            public override async ValueTask<Connection> AcceptAsync(IConnectionProperties options = null, CancellationToken cancellationToken = default)
            {
                var connection = await _socket.AcceptAsync();
                return Connection.FromStream(new NetworkStream(connection), leaveOpen: false, localEndPoint: connection.LocalEndPoint, remoteEndPoint: connection.RemoteEndPoint);
            }
        }

        private class SocketsConnectionListenerFactory : ConnectionListenerFactory
        {
            private SocketsTransport socketsTransport;

            public SocketsConnectionListenerFactory(SocketsTransport socketsTransport)
            {
                this.socketsTransport = socketsTransport;
            }

            public override ValueTask<ConnectionListener> ListenAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
            {
                return socketsTransport.ListenAsync(endPoint, options, cancellationToken);
            }
        }

        private class SocketsConnectionFactory : ConnectionFactory
        {
            private SocketsTransport socketsTransport;

            public SocketsConnectionFactory(SocketsTransport socketsTransport)
            {
                this.socketsTransport = socketsTransport;
            }

            public override ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
            {
                return socketsTransport.ConnectAsync(endPoint);
            }
        }
    }
}
