#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct PartitionResponseV0
    {
        public readonly PartitionHeaderV0 Header;
        public readonly int BatchSize;
        public readonly IEnumerable<RecordV0> Records;

        public PartitionResponseV0(ref SequenceReader<byte> reader)
        {
            this.Header = new PartitionHeaderV0(ref reader);

            this.BatchSize = reader.ReadInt32BigEndian();
            var messageBatchSequence = reader.Sequence.Slice(reader.Consumed, this.BatchSize);
            var mesageBatchReader = new SequenceReader<byte>(messageBatchSequence);

            this.Records = ParseRecords(ref mesageBatchReader);

            reader.Advance(this.BatchSize);
        }

        private static RecordV0[] ParseRecords(ref SequenceReader<byte> reader)
        {
            var records = new List<RecordV0>();
            bool parsingRecords = true;

            while (reader.Remaining > 0 && parsingRecords)
            {
                var offset = reader.ReadInt64BigEndian();

                if (offset == -1)
                {
                    // There are bytes left, but can't find anything describing them.
                    parsingRecords = false;
                    continue;
                }

                var messageSize = reader.ReadInt32BigEndian();

                Debug.Assert(messageSize + sizeof(int) + sizeof(long) <= reader.Remaining);

                var crc = reader.ReadInt32BigEndian();
                var magic = reader.ReadByte();
                var attributes = reader.ReadByte();

                byte[]? key = reader.ReadBytes();
                byte[]? value = reader.ReadBytes();

                var record = new RecordV0(
                    offset,
                    messageSize,
                    crc,
                    magic,
                    attributes,
                    key,
                    value);

                records.Add(record);
            }

            return records.ToArray();
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is PartitionResponseV0))
            {
                return false;
            }

            var that = (PartitionResponseV0)obj;

            return this.BatchSize.Equals(that.BatchSize)
                && this.Header.Equals(that.Header)
                && this.Records.Equals(that.Records);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.BatchSize,
                this.Header,
                this.Records);
        }

        public static bool operator ==(PartitionResponseV0 left, PartitionResponseV0 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PartitionResponseV0 left, PartitionResponseV0 right)
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
