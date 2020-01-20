using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct AbortedTransaction
    {
        public readonly long ProducerId;
        public readonly long FirstOffset;

        public AbortedTransaction(ref SequenceReader<byte> reader)
        {
            this.ProducerId = reader.ReadInt64BigEndian();
            this.FirstOffset = reader.ReadInt64BigEndian();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is AbortedTransaction))
            {
                return false;
            }

            var that = (AbortedTransaction)obj;

            return this.ProducerId.Equals(that.ProducerId)
                && this.FirstOffset.Equals(this.FirstOffset);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.ProducerId,
                this.FirstOffset);
        }

        public static bool operator ==(AbortedTransaction left, AbortedTransaction right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AbortedTransaction left, AbortedTransaction right)
        {
            return !(left == right);
        }
    }
}

// https://kafka.apache.org/protocol#The_Messages_Fetch
/*
 Fetch Response (Version: 11) => throttle_time_ms error_code session_id [responses] 
  throttle_time_ms => INT32
  error_code => INT16
  session_id => INT32
  responses => topic [partition_responses] 
    topic => STRING
    partition_responses => partition_header record_set 
      partition_header => partition error_code high_watermark last_stable_offset log_start_offset [aborted_transactions] preferred_read_replica 
        partition => INT32
        error_code => INT16
        high_watermark => INT64
        last_stable_offset => INT64
        log_start_offset => INT64
        aborted_transactions => producer_id first_offset 
          producer_id => INT64
          first_offset => INT64
        preferred_read_replica => INT32
      record_set => RECORDS
*/
