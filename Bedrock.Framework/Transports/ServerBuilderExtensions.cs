using System;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace Bedrock.Framework
{
    public static partial class ServerBuilderExtensions
    {
        public static ServerBuilder ListenWebSocket(this ServerBuilder builder, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return builder.Listen<WebSocketConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static ServerBuilder ListenHttp2(this ServerBuilder builder, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return builder.Listen<Http2ConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
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
            builder.Bindings.Add(new EndPointBinding(endPoint, connectionBuilder.Build(), connectionListenerFactory));
            return builder;
        }
    }
}
