#nullable enable

using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct PartitionHeaderV0
    {
        public readonly int Partition;
        public readonly KafkaErrorCode ErrorCode;
        public readonly long HighWatermark;

        public PartitionHeaderV0(ref SequenceReader<byte> reader)
        {
            this.Partition = reader.ReadInt32BigEndian();
            this.ErrorCode = reader.ReadErrorCode();
            this.HighWatermark = reader.ReadInt64BigEndian();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is PartitionHeaderV0))
            {
                return false;
            }

            var that = (PartitionHeaderV0)obj;

            return this.ErrorCode.Equals(that.ErrorCode)
                && this.HighWatermark.Equals(that.HighWatermark)
                && this.Partition.Equals(that.Partition);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.ErrorCode,
                this.HighWatermark,
                this.Partition);
        }

        public static bool operator ==(PartitionHeaderV0 left, PartitionHeaderV0 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PartitionHeaderV0 left, PartitionHeaderV0 right)
        {
            return !(left == right);
        }
    }
}

// https://kafka.apache.org/protocol#The_Messages_Fetch
/*
Fetch Response (Version: 0) => [responses] 
  responses => topic [partition_responses] 
    topic => STRING
    partition_responses => partition_header record_set 
      partition_header => partition error_code high_watermark 
        partition => INT32
        error_code => INT16
        high_watermark => INT64
      record_set => RECORDS
*/
