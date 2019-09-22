using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace Bedrock.Framework
{
    public class ServerOptions : IServiceProvider
    {
        internal List<ServerBinding> Bindings { get; } = new List<ServerBinding>();

        public IServiceProvider ApplicationServices { get; set; }

        public void Listen<TTransport>(EndPoint endPoint, Action<IConnectionBuilder> configure) where TTransport : IConnectionListenerFactory
        {
            Listen(endPoint, ActivatorUtilities.CreateInstance<TTransport>(this), configure);
        }

        public void Listen(EndPoint endPoint, IConnectionListenerFactory connectionListenerFactory, Action<IConnectionBuilder> configure)
        {
            var connectionBuilder = new ConnectionBuilder(this);
            configure(connectionBuilder);
            Bindings.Add(new ServerBinding
            {
                EndPoint = endPoint,
                ServerApplication = connectionBuilder.Build(),
                ConnectionListenerFactory = connectionListenerFactory
            });
        }

        object IServiceProvider.GetService(Type serviceType)
        {
            return ApplicationServices?.GetService(serviceType);
        }

        public class ServerBinding
        {
            public EndPoint EndPoint { get; set; }
            public IConnectionListenerFactory ConnectionListenerFactory { get; set; }
            public ConnectionDelegate ServerApplication { get; set; }
        }
    }
}
