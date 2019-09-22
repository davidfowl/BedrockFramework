using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;

namespace Bedrock.Framework
{
    public static class ServerOptionsExtensions
    {
        public static ServerOptions Listen(this ServerOptions options, IPEndPoint endPoint, Action<IConnectionBuilder> configure)
        {
            return options.Listen<SocketTransportFactory>(endPoint, configure);
        }

        public static ServerOptions Listen(this ServerOptions options, IPAddress address, int port, Action<IConnectionBuilder> configure)
        {
            return options.Listen<SocketTransportFactory>(new IPEndPoint(address, port), configure);
        }

        public static ServerOptions ListenAnyIP(this ServerOptions options, int port, Action<IConnectionBuilder> configure)
        {
            return options.Listen<SocketTransportFactory>(new IPEndPoint(IPAddress.Any, port), configure);
        }

        public static ServerOptions ListenLocalhost(this ServerOptions options, int port, Action<IConnectionBuilder> configure)
        {
            return options.Listen<SocketTransportFactory>(new IPEndPoint(IPAddress.Loopback, port), configure);
        }

        public static ServerOptions ListenUnixSocket(this ServerOptions options, string socketPath, Action<IConnectionBuilder> configure)
        {
            return options.Listen<SocketTransportFactory>(new UnixDomainSocketEndPoint(socketPath), configure);
        }

        public static ServerOptions ListenWebSocket(this ServerOptions options, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return options.Listen<WebSocketConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static ServerOptions ListenHttp2(this ServerOptions options, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return options.Listen<Http2ConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static ServerOptions ListenSocket(this ServerOptions options, EndPoint endpoint, Action<IConnectionBuilder> serverApplication)
        {
            return options.Listen<SocketTransportFactory>(endpoint, serverApplication);
        }

        public static ServerOptions ListenAzureSignalR(this ServerOptions options, string connectionString, string hub, Action<IConnectionBuilder> serverApplication)
        {
            return options.Listen<AzureSignalRConnectionListenerFactory>(
                    new AzureSignalREndPoint(connectionString, hub, AzureSignalREndpointType.Server),
                    serverApplication);
        }

    }
}
