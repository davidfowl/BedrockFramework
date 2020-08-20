using System;
using System.Net;
using System.Net.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace Bedrock.Framework
{
    public static partial class ServerBuilderExtensions
    {
        public static ServerBuilder Listen<TTransport>(this ServerBuilder builder, EndPoint endPoint, Action<IConnectionBuilder> configure) where TTransport : ConnectionListenerFactory
        {
            return builder.Listen(endPoint, ActivatorUtilities.CreateInstance<TTransport>(builder.ApplicationServices), configure);
        }

        public static ServerBuilder Listen(this ServerBuilder builder, EndPoint endPoint, ConnectionListenerFactory connectionListenerFactory, Action<IConnectionBuilder> configure)
        {
            var connectionBuilder = new ConnectionBuilder(builder.ApplicationServices);
            configure(connectionBuilder);
            builder.Bindings.Add(new EndPointBinding(endPoint, connectionBuilder.Build(), connectionListenerFactory));
            return builder;
        }
    }
}
