#nullable enable

using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct MetadataTopicV0
    {
        public readonly KafkaErrorCode ErrorCode;
        public readonly string Name;
        public readonly MetadataPartitionV0[] Partitions;

        public MetadataTopicV0(
            KafkaErrorCode error,
            string name,
            MetadataPartitionV0[] partitions)
        {
            this.ErrorCode = error;
            this.Name = name;
            this.Partitions = partitions;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is MetadataTopicV0))
            {
                return false;
            }

            var that = (MetadataTopicV0)obj;

            return this.ErrorCode.Equals(that.ErrorCode)
                && this.Name.Equals(that.Name)
                && this.Partitions.Equals(that.Partitions);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.ErrorCode,
                this.Name,
                this.Partitions);
        }

        public static bool operator ==(MetadataTopicV0 left, MetadataTopicV0 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MetadataTopicV0 left, MetadataTopicV0 right)
        {
            return !(left == right);
        }
    }
}
