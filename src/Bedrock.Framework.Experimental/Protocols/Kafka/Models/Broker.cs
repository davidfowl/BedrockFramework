#nullable enable

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct Broker
    {
        public readonly int NodeId;
        public readonly string Host;
        public readonly int Port;
        public readonly string? Rack;

        public Broker(int nodeId, string host, int port, string? rack)
        {
            this.NodeId = nodeId;
            this.Host = host;
            this.Port = port;
            this.Rack = rack;
        }
    }
}

#nullable restore