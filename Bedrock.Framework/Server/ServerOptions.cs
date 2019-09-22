using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public class ServerOptions : IServiceProvider
    {
        internal List<ServerBinding> Bindings { get; } = new List<ServerBinding>();

        public ServerOptions()
        {
        }

        public ServerOptions(IServiceProvider serviceProvider)
        {
            ApplicationServices = serviceProvider;
        }

        public IServiceProvider ApplicationServices { get; set; }

        public ServerOptions Listen(EndPoint endPoint, IConnectionListenerFactory connectionListenerFactory, Action<IConnectionBuilder> configure)
        {
            var connectionBuilder = new ConnectionBuilder(this);
            configure(connectionBuilder);
            Bindings.Add(new ServerBinding
            {
                EndPoint = endPoint,
                ServerApplication = connectionBuilder.Build(),
                ConnectionListenerFactory = connectionListenerFactory
            });
            return this;
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
