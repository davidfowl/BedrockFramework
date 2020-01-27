#nullable enable

using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct RecordV0
    {
        public readonly long Offset;
        public readonly int MessageSize;
        public readonly int Crc;
        public readonly byte Magic;
        public readonly byte Attributes;
        public readonly byte[]? Key;
        public readonly byte[]? Value;

        public RecordV0(
            long offset,
            int messageSize,
            int crc,
            byte magic,
            byte attributes,
            byte[]? key,
            byte[]? value)
        {
            this.Offset = offset;
            this.MessageSize = messageSize;
            this.Crc = crc;
            this.Magic = magic;
            this.Attributes = attributes;
            this.Key = key;
            this.Value = value;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is RecordV0))
            {
                return false;
            }

            var that = (RecordV0)obj;

            return this.GetHashCode().Equals(that.GetHashCode());
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.Offset,
                this.MessageSize,
                this.Crc,
                this.Magic,
                this.Attributes,
                this.Key,
                this.Value);
        }

        public static bool operator ==(RecordV0 left, RecordV0 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RecordV0 left, RecordV0 right)
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
