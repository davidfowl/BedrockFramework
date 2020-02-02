#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests
{
    public class MetadataRequestV0 : KafkaRequest
    {
        public static MetadataRequestV0 AllTopics = new MetadataRequestV0();

        public MetadataRequestV0()
            : base(KafkaApiKeys.Metadata, apiVersion: 0)
        {
        }

        public string[]? Topics { get; set; }

        public override void WriteRequest<TStrategy>(ref StrategyPayloadWriter<TStrategy> writer)
        {
            writer.WriteArray(this.Topics, this.WriteTopic);
        }

        private StrategyPayloadWriterContext<TStrategy> WriteTopic<TStrategy>(string topicName, StrategyPayloadWriterContext<TStrategy> context)
            where TStrategy : struct, IPayloadWriterStrategy
        {
            var writer = context.CreatePayloadWriter();
            writer.WriteString(topicName);

            return writer.Context;
        }
    }
}
