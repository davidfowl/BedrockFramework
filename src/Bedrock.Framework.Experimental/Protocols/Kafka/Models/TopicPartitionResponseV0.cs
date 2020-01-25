#nullable enable

using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct TopicPartitionResponseV0
    {
        public readonly string Topic;
        public readonly PartitionResponseV0[] PartitionResponses;

        public TopicPartitionResponseV0(string topic, PartitionResponseV0[] partitionResponses)
        {
            this.Topic = topic;
            this.PartitionResponses = partitionResponses;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is TopicPartitionResponseV0))
            {
                return false;
            }

            var that = (TopicPartitionResponseV0)obj;

            return this.Topic.Equals(that.Topic)
                && this.PartitionResponses.Equals(that.PartitionResponses);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.Topic,
                this.PartitionResponses);
        }

        public static bool operator ==(TopicPartitionResponseV0 left, TopicPartitionResponseV0 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TopicPartitionResponseV0 left, TopicPartitionResponseV0 right)
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
