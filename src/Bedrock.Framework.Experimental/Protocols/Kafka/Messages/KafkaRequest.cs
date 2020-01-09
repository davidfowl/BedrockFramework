using Bedrock.Framework.Infrastructure;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages
{
    public abstract class KafkaRequest
    {
        public KafkaRequest(KafkaApiKeys apiKey, short apiVersion)
        {
            this.ApiKey = apiKey;
            this.ApiVersion = apiVersion;
        }

        public KafkaApiKeys ApiKey { get; }
        public short ApiVersion { get; }

        public abstract void WriteRequest(ref BufferWriter<IBufferWriter<byte>> output);

        public virtual int GetPayloadSize()
            => 0; // Some requests have no body.
    }
}
