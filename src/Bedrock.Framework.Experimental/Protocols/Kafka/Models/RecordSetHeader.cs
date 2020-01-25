#nullable enable

using System;
using System.Buffers;
using System.Diagnostics;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public struct RecordSetHeader
    {
        public long BaseOffset;
        public int BatchLength;
        public int PartitionLeaderEpoch;
        public byte Magic; //current magic value is 2
        public int Crc; // CRC is from attributes to end of payload.

        //    bit 0~2:
        //        0: no compression
        //        1: gzip
        //        2: snappy
        //        3: lz4
        //        4: zstd
        //    bit 3: timestampType
        //    bit 4: isTransactional(0 means not transactional)
        //    bit 5: isControlBatch(0 means not a control batch)
        //    bit 6~15: unused
        public short Attributes;
        public int LastOffsetDelta;
        public long FirstTimestamp;
        public long MaxTimestamp;
        public long ProducerId;
        public short ProducerEpoch;
        public int BaseSequence;

        public RecordSetHeader(ref SequenceReader<byte> reader)
        {
            this.BaseOffset = reader.ReadInt64BigEndian();
            this.BatchLength = reader.ReadInt32BigEndian();
            this.PartitionLeaderEpoch = reader.ReadInt32BigEndian();
            this.Magic = reader.ReadByte();
            Debug.Assert(this.Magic == 2);
            this.Crc = reader.ReadInt32BigEndian();
            this.Attributes = reader.ReadInt16BigEndian();
            this.LastOffsetDelta = reader.ReadInt32BigEndian();
            this.FirstTimestamp = reader.ReadInt64BigEndian();
            this.MaxTimestamp = reader.ReadInt64BigEndian();
            this.ProducerId = reader.ReadInt64BigEndian();
            this.ProducerEpoch = reader.ReadInt16BigEndian();
            this.BaseSequence = reader.ReadInt32BigEndian();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is RecordSetHeader))
            {
                return false;
            }

            var that = (RecordSetHeader)obj;

            return this.GetHashCode().Equals(that.GetHashCode());
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                HashCode.Combine(
                    this.Attributes,
                    this.BaseOffset,
                    this.BaseSequence,
                    this.BatchLength,
                    this.Crc,
                    this.FirstTimestamp,
                    this.LastOffsetDelta,
                    this.Magic),
                HashCode.Combine(
                    this.MaxTimestamp,
                    this.PartitionLeaderEpoch,
                    this.ProducerEpoch,
                    this.ProducerId));
        }

        public static bool operator ==(RecordSetHeader left, RecordSetHeader right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RecordSetHeader left, RecordSetHeader right)
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
