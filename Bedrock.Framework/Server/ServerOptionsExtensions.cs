using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bedrock.Framework
{
    public static class ServerOptionsExtensions
    {
        public static ServerOptions Listen(this ServerOptions options, EndPoint endPoint, Action<IConnectionBuilder> configure)
        {
            var socketOptions = new SocketTransportOptions();
            var loggerFactory = options.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            var socketTransportFactory = new SocketTransportFactory(Options.Create(socketOptions), loggerFactory);
            return options.Listen(endPoint, socketTransportFactory, configure);
        }

        public static ServerOptions Listen(this ServerOptions options, IPAddress address, int port, Action<IConnectionBuilder> configure)
        {
            return options.Listen(new IPEndPoint(address, port), configure);
        }

        public static ServerOptions ListenAnyIP(this ServerOptions options, int port, Action<IConnectionBuilder> configure)
        {
            return options.Listen(IPAddress.Any, port, configure);
        }

        public static ServerOptions ListenLocalhost(this ServerOptions options, int port, Action<IConnectionBuilder> configure)
        {
            return options.Listen(IPAddress.Loopback, port, configure);
        }

        public static ServerOptions ListenUnixSocket(this ServerOptions options, string socketPath, Action<IConnectionBuilder> configure)
        {
            return options.Listen(new UnixDomainSocketEndPoint(socketPath), configure);
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

        public static ServerOptions Listen<TTransport>(this ServerOptions options, EndPoint endPoint, Action<IConnectionBuilder> configure) where TTransport : IConnectionListenerFactory
        {
            return options.Listen(endPoint, ActivatorUtilities.CreateInstance<TTransport>(options), configure);
        }
        
        public static ServerOptions Listen(this ServerOptions options, EndPoint endPoint, IConnectionListenerFactory connectionListenerFactory, Action<IConnectionBuilder> configure)
        {
            var connectionBuilder = new ConnectionBuilder(options);
            configure(connectionBuilder);
            options.Bindings.Add(new ServerBinding(endPoint, connectionBuilder.Build(), connectionListenerFactory));
            return options;
        }
    }
}
