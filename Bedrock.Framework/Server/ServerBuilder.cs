using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bedrock.Framework
{
    public class ServerBuilder
    {
        public ServerBuilder(IServiceProvider serviceProvider)
        {
            ApplicationServices = serviceProvider;
        }

        internal ILoggerFactory LoggerFactory => (ILoggerFactory)ApplicationServices?.GetService(typeof(ILoggerFactory)) ?? NullLoggerFactory.Instance;

        public IList<ServerBinding> Bindings { get; } = new List<ServerBinding>();

        public TimeSpan GracefulShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public TimeSpan HeartBeatInterval { get; set; } = TimeSpan.FromSeconds(1);

        public IServiceProvider ApplicationServices { get; }

        public Server Build()
        {
            return new Server(this);
        }
    }
}
