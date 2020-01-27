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

        public override void WriteRequest(ref PayloadWriter writer)
        {
            writer.WriteArray(this.Topics, this.WriteTopic);
        }

        private PayloadWriterContext WriteTopic(string topicName, PayloadWriterContext context)
        {
            var writer = context.CreatePayloadWriter();
            writer.WriteString(topicName);

            return writer.Context;
        }
    }
}
