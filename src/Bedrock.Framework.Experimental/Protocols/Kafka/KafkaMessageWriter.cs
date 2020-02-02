#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests;
using Bedrock.Framework.Experimental.Protocols.Kafka.Services;
using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Protocols;
using Microsoft.Extensions.Logging;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public class KafkaMessageWriter : IMessageWriter<KafkaRequest>
    {
        private readonly IMessageCorrelator correlator;
        private readonly ILogger<KafkaMessageWriter> logger;

        public KafkaMessageWriter(
            IMessageCorrelator messageCorrelator,
            ILogger<KafkaMessageWriter> logger)
        {
            this.correlator = messageCorrelator;
            this.logger = logger;
        }

        public void WriteMessage(KafkaRequest message, IBufferWriter<byte> output)
        {
            var correlationId = this.correlator.GetCorrelationId(message);
            var writer = new BufferWriter<IBufferWriter<byte>>(output);
            var clientId = message.ClientId;

            // On every outgoing message...
            var pw = new StrategyPayloadWriter<BigEndianStrategy>()
                .StartCalculatingSize("payloadSize")
                    .Write((short)message.ApiKey)
                    .Write(message.ApiVersion)
                    .Write(correlationId)
                    .WriteNullableString(ref clientId);

            // Since each Kafka Request/Response is versioned, have those objects
            // write/read on the PayloadWriter itself.
            message.WriteRequest(ref pw);

            pw.EndSizeCalculation("payloadSize");
            
            if (!pw.TryWritePayload(out var payload))
            {
                this.logger.LogError("Unable to retrieve payload for {KafkaRequest}", message);
            }

            writer.Write(payload.ToSpan());

            writer.Commit();
        }
    }
}
