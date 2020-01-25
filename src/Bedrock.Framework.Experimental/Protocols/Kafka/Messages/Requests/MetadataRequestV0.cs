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

        public string[]? Topics { get; set; } = Array.Empty<string>();

        public override void WriteRequest(ref PayloadWriter writer)
        {
            if (this.Topics == null)
            {
                writer.WriteArrayPreamble(null);
            }
            else
            {
                var topicCount = this.Topics.Length;
                writer.WriteArrayPreamble(topicCount);

                for (int i = 0; i < topicCount; i++)
                {
                    var topic = this.Topics[i];
                    writer.WriteString(topic);
                }
            }
        }
    }
}
