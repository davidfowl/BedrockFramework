#nullable enable

using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct MetadataBrokerV0
    {
        public readonly int NodeId;
        public readonly string Host;
        public readonly int Port;

        public MetadataBrokerV0(int nodeId, string host, int port)
        {
            this.NodeId = nodeId;
            this.Host = host;
            this.Port = port;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is MetadataBrokerV0))
            {
                return false;
            }

            var that = (MetadataBrokerV0)obj;

            return this.NodeId.Equals(that.NodeId)
                && this.Host.Equals(that.Host)
                && this.Port.Equals(that.Port);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.NodeId,
                this.Host,
                this.Port);
        }

        public static bool operator ==(MetadataBrokerV0 left, MetadataBrokerV0 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MetadataBrokerV0 left, MetadataBrokerV0 right)
        {
            return !(left == right);
        }
    }
}
