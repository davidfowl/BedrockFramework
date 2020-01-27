#nullable enable

using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct TopicPartition
    {
        public readonly string TopicName;
        public readonly Partition Partition;

        public TopicPartition(string topicName, Partition partition)
        {
            this.TopicName = topicName;
            this.Partition = partition;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is TopicPartition))
            {
                return false;
            }

            var that = (TopicPartition)obj;

            return this.Partition.Equals(that.Partition)
                && this.TopicName.Equals(that.TopicName);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.TopicName,
                this.Partition);
        }

        public static bool operator ==(TopicPartition left, TopicPartition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TopicPartition left, TopicPartition right)
        {
            return !(left == right);
        }
    }
}
