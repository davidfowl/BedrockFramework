using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;

namespace Bedrock.Framework
{
    public static class ServerOptionsExtensions
    {
        public static void Listen(this ServerOptions options, IPEndPoint endPoint, Action<IConnectionBuilder> configure)
        {
            options.Listen<SocketTransportFactory>(endPoint, configure);
        }

        public static void Listen(this ServerOptions options, IPAddress address, int port, Action<IConnectionBuilder> configure)
        {
            options.Listen<SocketTransportFactory>(new IPEndPoint(address, port), configure);
        }

        public static void ListenAnyIP(this ServerOptions options, int port, Action<IConnectionBuilder> configure)
        {
            options.Listen<SocketTransportFactory>(new IPEndPoint(IPAddress.Any, port), configure);
        }

        public static void ListenLocalhost(this ServerOptions options, int port, Action<IConnectionBuilder> configure)
        {
            options.Listen<SocketTransportFactory>(new IPEndPoint(IPAddress.Loopback, port), configure);
        }

        public static void ListenUnixSocket(this ServerOptions options, string socketPath, Action<IConnectionBuilder> configure)
        {
            options.Listen<SocketTransportFactory>(new UnixDomainSocketEndPoint(socketPath), configure);
        }

        public static void ListenWebSocket(this ServerOptions options, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            options.Listen<WebSocketConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static void ListenHttp2(this ServerOptions options, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            options.Listen<Http2ConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static void ListenSocket(this ServerOptions options, EndPoint endpoint, Action<IConnectionBuilder> serverApplication)
        {
            options.Listen<SocketTransportFactory>(endpoint, serverApplication);
        }

        public static void ListenAzureSignalR(this ServerOptions options, string connectionString, string hub, Action<IConnectionBuilder> serverApplication)
        {
            options.Listen<AzureSignalRConnectionListenerFactory>(
                new AzureSignalREndPoint(connectionString, hub, AzureSignalREndpointType.Server),
                serverApplication);
        }

    }
}
