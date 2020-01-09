using Bedrock.Framework.Experimental.Protocols.Kafka.Messages;
using Bedrock.Framework.Experimental.Protocols.Kafka.Primitives;
using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public class KafkaMessageWriter : IMessageWriter<KafkaRequest>
    {
        private int headerSize = 
            sizeof(short) // ApiKey
            + sizeof(short) // ApiVersion
            + sizeof(int); // CorrelationId

        private int correlationId = 1;
        private readonly ConcurrentDictionary<int, KafkaRequest> correlations;
        private readonly string clientId;
        private NullableString clientIdNullable;

        public KafkaMessageWriter(ConcurrentDictionary<int, KafkaRequest> correlations, string clientId)
        {
            this.correlations = correlations ?? new ConcurrentDictionary<int, KafkaRequest>();

            this.clientId = clientId;
            this.clientIdNullable = new NullableString(clientId);

            this.headerSize += this.clientIdNullable.Size;
        }

        public void WriteMessage(KafkaRequest message, IBufferWriter<byte> output)
        {
            var correlationId = this.correlationId++;
            if (!this.correlations.TryAdd(correlationId, message))
            {
                throw new InvalidOperationException($"Non-unique correlationId provided in {message.GetType().Name}");
            }

            var writer = new BufferWriter<IBufferWriter<byte>>(output);

            var payloadSize = message.GetPayloadSize() + headerSize;

            // On every outgoing message...
            writer.WriteInt32BigEndian(payloadSize);
            writer.WriteInt16BigEndian((short)message.ApiKey);
            writer.WriteInt16BigEndian(message.ApiVersion);
            writer.WriteInt32BigEndian(correlationId);
            writer.WriteNullableString(ref clientIdNullable);

            // Since each Kafka Request/Response is versioned, have those objects
            // write/read on the buffer themselves.
            message.WriteRequest(ref writer);

            writer.Commit();
        }
    }
}
