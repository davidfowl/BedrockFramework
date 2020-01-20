#nullable enable

using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct MetadataPartitionV0
    {
        public readonly KafkaErrorCode ErrorCode;
        public readonly int PartitionIndex;
        public readonly int LeaderId;
        public readonly int[] ReplicaNodes;
        public readonly int[] IsrNodes;

        public MetadataPartitionV0(
            KafkaErrorCode errorCode,
            int partitionIndex,
            int leaderId,
            int[] replicaNodes,
            int[] isrNodes)
        {
            this.ErrorCode = errorCode;
            this.PartitionIndex = partitionIndex;
            this.LeaderId = leaderId;
            this.ReplicaNodes = replicaNodes;
            this.IsrNodes = isrNodes;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is MetadataPartitionV0))
            {
                return false;
            }

            var that = (MetadataPartitionV0)obj;

            return this.GetHashCode().Equals(that.GetHashCode());
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.ErrorCode,
                this.PartitionIndex,
                this.ReplicaNodes,
                this.LeaderId,
                this.IsrNodes);
        }

        public static bool operator ==(MetadataPartitionV0 left, MetadataPartitionV0 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MetadataPartitionV0 left, MetadataPartitionV0 right)
        {
            return !(left == right);
        }
    }
}
