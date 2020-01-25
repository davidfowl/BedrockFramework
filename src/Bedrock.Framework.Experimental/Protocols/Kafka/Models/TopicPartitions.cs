#nullable enable

using System;
using System.Linq;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct TopicPartitions
    {
        public readonly string TopicName;
        public readonly Partition[] Partitions;

        public TopicPartitions(string topic, Partition[] partitions)
        {
            this.TopicName = topic;

            // TODO: Distinct these
            this.Partitions = partitions
                .Distinct()
                .ToArray();
        }
        public TopicPartitions(string topic, Partition partition)
            : this(topic, new[] { partition })
        {
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is TopicPartitions))
            {
                return false;
            }

            var that = (TopicPartitions)obj;

            return this.Partitions.Equals(that.Partitions)
                && this.TopicName.Equals(that.TopicName);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.TopicName,
                this.Partitions);
        }

        public static bool operator ==(TopicPartitions left, TopicPartitions right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TopicPartitions left, TopicPartitions right)
        {
            return !(left == right);
        }
    }
}
