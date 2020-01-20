using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests;
using Bedrock.Framework.Experimental.Protocols.Kafka.Services;
using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Protocols;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public class KafkaMessageWriter : IMessageWriter<KafkaRequest>
    {
        private readonly int headerSize = 
            sizeof(short) // ApiKey
            + sizeof(short) // ApiVersion
            + sizeof(int); // CorrelationId

        private readonly IMessageCorrelator correlator;

        public KafkaMessageWriter(IMessageCorrelator messageCorrelator)
        {
            this.correlator = messageCorrelator;
        }

        public void WriteMessage(KafkaRequest message, IBufferWriter<byte> output)
        {
            var correlationId = this.correlator.GetCorrelationId(message);
            var writer = new BufferWriter<IBufferWriter<byte>>(output);
            var clientId = message.ClientId;

            var payloadSize = message.GetPayloadSize()
                + headerSize
                + clientId.Size;

            // On every outgoing message...
            writer.WriteInt32BigEndian(payloadSize);
            writer.WriteInt16BigEndian((short)message.ApiKey);
            writer.WriteInt16BigEndian(message.ApiVersion);
            writer.WriteInt32BigEndian(correlationId);
            writer.WriteNullableString(ref clientId);

            // Since each Kafka Request/Response is versioned, have those objects
            // write/read on the buffer themselves.
            message.WriteRequest(ref writer);

            writer.Commit();
        }
    }
}
