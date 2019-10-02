using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bedrock.Framework
{
    public static class ServerOptionsExtensions
    {
        public static ServerBuilder Listen(this ServerBuilder options, EndPoint endPoint, Action<IConnectionBuilder> configure)
        {
            var socketOptions = new SocketTransportOptions();
            var socketTransportFactory = new SocketTransportFactory(Options.Create(socketOptions), options.LoggerFactory);
            return options.Listen(endPoint, socketTransportFactory, configure);
        }

        public static ServerBuilder Listen(this ServerBuilder options, IPAddress address, int port, Action<IConnectionBuilder> configure)
        {
            return options.Listen(new IPEndPoint(address, port), configure);
        }

        public static ServerBuilder ListenAnyIP(this ServerBuilder options, int port, Action<IConnectionBuilder> configure)
        {
            return options.Listen(IPAddress.Any, port, configure);
        }

        public static ServerBuilder ListenLocalhost(this ServerBuilder options, int port, Action<IConnectionBuilder> configure)
        {
            return options.Listen(IPAddress.Loopback, port, configure);
        }

        public static ServerBuilder ListenUnixSocket(this ServerBuilder options, string socketPath, Action<IConnectionBuilder> configure)
        {
            return options.Listen(new UnixDomainSocketEndPoint(socketPath), configure);
        }

        public static ServerBuilder ListenWebSocket(this ServerBuilder options, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return options.Listen<WebSocketConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static ServerBuilder ListenHttp2(this ServerBuilder options, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return options.Listen<Http2ConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static ServerBuilder ListenSocket(this ServerBuilder options, EndPoint endpoint, Action<IConnectionBuilder> serverApplication)
        {
            return options.Listen<SocketTransportFactory>(endpoint, serverApplication);
        }

        public static ServerBuilder ListenAzureSignalR(this ServerBuilder options, string connectionString, string hub, Action<IConnectionBuilder> serverApplication)
        {
            return options.Listen<AzureSignalRConnectionListenerFactory>(
                    new AzureSignalREndPoint(connectionString, hub, AzureSignalREndpointType.Server),
                    serverApplication);
        }

        public static ServerBuilder Listen<TTransport>(this ServerBuilder options, EndPoint endPoint, Action<IConnectionBuilder> configure) where TTransport : IConnectionListenerFactory
        {
            return options.Listen(endPoint, ActivatorUtilities.CreateInstance<TTransport>(options.ApplicationServices), configure);
        }
        
        public static ServerBuilder Listen(this ServerBuilder options, EndPoint endPoint, IConnectionListenerFactory connectionListenerFactory, Action<IConnectionBuilder> configure)
        {
            var connectionBuilder = new ConnectionBuilder(options.ApplicationServices);
            configure(connectionBuilder);
            options.Bindings.Add(new ServerBinding(endPoint, connectionBuilder.Build(), connectionListenerFactory));
            return options;
        }
    }
}
