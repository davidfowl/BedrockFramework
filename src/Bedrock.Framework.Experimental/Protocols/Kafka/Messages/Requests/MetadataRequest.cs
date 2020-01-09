#nullable enable

using Bedrock.Framework.Infrastructure;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests
{
    public class MetadataRequest : KafkaRequest
    {
        public MetadataRequest()
            : base(KafkaApiKeys.Metadata, apiVersion: 8)
        {
        }

        public List<string> Topics { get; set; } = new List<string>();
        public bool AllowAutoTopicCreation { get; set; }
        public bool IncludeClusterAuthorizedOperations { get; set; }
        public bool IncludeTopicAuthorizedOperations { get; set; }

        public override int GetPayloadSize()
        {
            // Todo: speed this up, we'll traverse each string twice.
            return sizeof(byte) * 3 // three bools
                + sizeof(int) // topic array size value
                + sizeof(short) * this.Topics.Count //  each short saying how long each string is
                + this.Topics.Sum(t => Encoding.UTF8.GetByteCount(t));
        }

        public override void WriteRequest(ref BufferWriter<IBufferWriter<byte>> writer)
        {
            var topicCount = Topics.Count();
            writer.WriteArrayPreamble(topicCount);

            using var enumerator = Topics.GetEnumerator();

            while (enumerator.MoveNext())
            {
                writer.WriteString(enumerator.Current);
            }

            writer.WriteBoolean(this.AllowAutoTopicCreation);
            writer.WriteBoolean(this.IncludeClusterAuthorizedOperations);
            writer.WriteBoolean(this.IncludeTopicAuthorizedOperations);
        }
    }
}

#nullable restore