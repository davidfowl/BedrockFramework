using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bedrock.Framework
{
    public static class ServerBuilderExtensions
    {
        public static ServerBuilder Listen(this ServerBuilder builder, EndPoint endPoint, Action<IConnectionBuilder> configure)
        {
            var socketOptions = new SocketTransportOptions();
            var socketTransportFactory = new SocketTransportFactory(Options.Create(socketOptions), builder.ApplicationServices.GetLoggerFactory());
            return builder.Listen(endPoint, socketTransportFactory, configure);
        }

        public static ServerBuilder Listen(this ServerBuilder builder, IPAddress address, int port, Action<IConnectionBuilder> configure)
        {
            return builder.Listen(new IPEndPoint(address, port), configure);
        }

        public static ServerBuilder ListenAnyIP(this ServerBuilder builder, int port, Action<IConnectionBuilder> configure)
        {
            return builder.Listen(IPAddress.Any, port, configure);
        }

        public static ServerBuilder ListenLocalhost(this ServerBuilder builder, int port, Action<IConnectionBuilder> configure)
        {
            return builder.Listen(IPAddress.Loopback, port, configure);
        }

        public static ServerBuilder ListenUnixSocket(this ServerBuilder builder, string socketPath, Action<IConnectionBuilder> configure)
        {
            return builder.Listen(new UnixDomainSocketEndPoint(socketPath), configure);
        }

        public static ServerBuilder ListenWebSocket(this ServerBuilder builder, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return builder.Listen<WebSocketConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static ServerBuilder ListenHttp2(this ServerBuilder builder, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return builder.Listen<Http2ConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static ServerBuilder ListenSocket(this ServerBuilder builder, EndPoint endpoint, Action<IConnectionBuilder> serverApplication)
        {
            return builder.Listen<SocketTransportFactory>(endpoint, serverApplication);
        }

        public static ServerBuilder ListenAzureSignalR(this ServerBuilder builder, string connectionString, string hub, Action<IConnectionBuilder> serverApplication)
        {
            return builder.Listen<AzureSignalRConnectionListenerFactory>(
                    new AzureSignalREndPoint(connectionString, hub, AzureSignalREndpointType.Server),
                    serverApplication);
        }

        public static ServerBuilder Listen<TTransport>(this ServerBuilder builder, EndPoint endPoint, Action<IConnectionBuilder> configure) where TTransport : IConnectionListenerFactory
        {
            return builder.Listen(endPoint, ActivatorUtilities.CreateInstance<TTransport>(builder.ApplicationServices), configure);
        }

        public static ServerBuilder Listen(this ServerBuilder builder, EndPoint endPoint, IConnectionListenerFactory connectionListenerFactory, Action<IConnectionBuilder> configure)
        {
            var connectionBuilder = new ConnectionBuilder(builder.ApplicationServices);
            configure(connectionBuilder);
            builder.Bindings.Add(new ServerBinding(endPoint, connectionBuilder.Build(), connectionListenerFactory));
            return builder;
        }
    }
}
