using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses
{
    public class FetchResponseV0 : KafkaResponse
    {
        public TopicPartitionResponseV0[] Responses { get; private set; } = Array.Empty<TopicPartitionResponseV0>();

        public override void FillResponse(in ReadOnlySequence<byte> response)
        {
            var reader = new SequenceReader<byte>(response);
            this.Responses = this.ParseResponses(ref reader);
        }

        private TopicPartitionResponseV0[] ParseResponses(ref SequenceReader<byte> reader)
        {
            int numberOfTopics = reader.ReadInt32BigEndian();
            var topics = new TopicPartitionResponseV0[numberOfTopics];

            for (int i = 0; i < numberOfTopics; i++)
            {
                var topic = reader.ReadString();

                int numberOfPartitionResponses = reader.ReadInt32BigEndian();
                var partitionResponses = new PartitionResponseV0[numberOfPartitionResponses];

                for (int j = 0; j < numberOfPartitionResponses; j++)
                {
                    partitionResponses[j] = new PartitionResponseV0(ref reader);
                }

                topics[i] = new TopicPartitionResponseV0(topic, partitionResponses);
            }

            return topics;
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
