using System.Net;
using System.Net.Connections;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public class SocketsConnectionListenerFactory : ConnectionListenerFactory
    {
        public override ValueTask<ConnectionListener> ListenAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
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

    }

}
