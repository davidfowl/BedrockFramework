using System;
using System.Linq;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct FetchTopicV0
    {
        public readonly string Topic;
        public readonly FetchTopicPartitionV0[] Partitions { get; }

        public FetchTopicV0(string topic, FetchTopicPartitionV0[] partitions)
        {
            this.Topic = topic;
            this.Partitions = partitions;
        }

        private const int constantPayloadSize =
            sizeof(short) // topic name length
            + sizeof(int); // partition array length

        public int GetSize()
        {
            return constantPayloadSize
                + Encoding.UTF8.GetByteCount(this.Topic)
                + this.Partitions.Sum(p => p.GetSize());
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is FetchTopicV0))
            {
                return false;
            }

            var that = (FetchTopicV0)obj;

            return this.Topic.Equals(that.Topic)
                && this.Partitions.Equals(that.Partitions);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.Topic,
                this.Partitions);
        }

        public static bool operator ==(FetchTopicV0 left, FetchTopicV0 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FetchTopicV0 left, FetchTopicV0 right)
        {
            return !(left == right);
        }
    }
}

// https://kafka.apache.org/protocol#The_Messages_Fetch
/*
Fetch Request (Version: 0) => replica_id max_wait_time min_bytes [topics] 
  replica_id => INT32
  max_wait_time => INT32
  min_bytes => INT32
  topics => topic [partitions] 
    topic => STRING
    partitions => partition fetch_offset partition_max_bytes 
      partition => INT32
      fetch_offset => INT64
      partition_max_bytes => INT32
*/
