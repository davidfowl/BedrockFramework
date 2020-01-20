using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct FetchTopicPartitionV0
    {
        public readonly int Partition;
        public readonly long FetchOffset;
        public readonly int PartitionMaxBytes;

        public FetchTopicPartitionV0(
            int partition,
            long fetchOffset,
            int partitionMaxBytes)
        {
            this.Partition = partition;
            this.FetchOffset = fetchOffset;
            this.PartitionMaxBytes = partitionMaxBytes;
        }

        private const int constantPayloadSize =
            sizeof(int) // partition idx
            + sizeof(long) // fetch offset
            + sizeof(int); // max bytes to fetch from partition

        public int GetSize()
        {
            return constantPayloadSize;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is FetchTopicPartitionV0))
            {
                return false;
            }

            var that = (FetchTopicPartitionV0)obj;

            return this.FetchOffset.Equals(that.FetchOffset)
                && this.Partition.Equals(that.Partition)
                && this.PartitionMaxBytes.Equals(that.PartitionMaxBytes);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.FetchOffset,
                this.Partition,
                this.PartitionMaxBytes);
        }

        public static bool operator ==(FetchTopicPartitionV0 left, FetchTopicPartitionV0 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FetchTopicPartitionV0 left, FetchTopicPartitionV0 right)
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
