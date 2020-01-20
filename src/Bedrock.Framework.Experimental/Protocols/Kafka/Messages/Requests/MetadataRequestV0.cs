#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Linq;
using System.Text;

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

        private const int constantPayloadSize =
            sizeof(int); // topic array length

        public override int GetPayloadSize()
        {
            // Todo: speed this up, we'll traverse each string twice.
            return constantPayloadSize
                + (sizeof(short) * (this.Topics?.Length ?? 0)) // short for each topic saying how long each string is
                + (this.Topics?.Sum(t => Encoding.UTF8.GetByteCount(t)) ?? 0);
        }

        public override void WriteRequest(ref BufferWriter<IBufferWriter<byte>> writer)
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
