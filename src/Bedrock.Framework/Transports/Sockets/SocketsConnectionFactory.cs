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
    public class SocketsConnectionFactory : ConnectionFactory
    {
        public override async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            var socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(endPoint);
            var ns = new NetworkStream(socket);
            return Connection.FromStream(ns, localEndPoint: socket.LocalEndPoint, remoteEndPoint: socket.RemoteEndPoint);
        }
    }

}
