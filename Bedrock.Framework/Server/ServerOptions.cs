using System;
using System.Collections.Generic;

namespace Bedrock.Framework
{
    public partial class ServerOptions : IServiceProvider
    {
        public ServerOptions()
        {
        }

        public ServerOptions(IServiceProvider serviceProvider)
        {
            ApplicationServices = serviceProvider;
        }

        public IList<ServerBinding> Bindings { get; } = new List<ServerBinding>();

        public TimeSpan GracefulShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public IServiceProvider ApplicationServices { get; set; }

        object IServiceProvider.GetService(Type serviceType)
        {
            return ApplicationServices?.GetService(serviceType);
        }
    }
}
