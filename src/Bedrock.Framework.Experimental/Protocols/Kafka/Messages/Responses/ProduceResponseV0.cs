using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses
{
    public class ProduceResponseV0 : KafkaResponse
    {
        public TopicPartitionResponseV0[] Responses { get; private set; } = Array.Empty<TopicPartitionResponseV0>();

        public override void FillResponse(in ReadOnlySequence<byte> response)
        {
            var reader = new SequenceReader<byte>(response);
        }
    }
}