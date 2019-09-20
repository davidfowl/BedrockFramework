using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Connections;

namespace BedrockTransports
{
    public class ServerApplicationOptions
    {
        public List<ServerBinding> Bindings { get; set; } = new List<ServerBinding>();
    }

    public class ServerBinding
    {
        public EndPoint EndPoint { get; set; }
        public IConnectionListenerFactory ConnectionListenerFactory { get; set; }
        public ConnectionDelegate ServerApplication { get; set; }
    }
}
