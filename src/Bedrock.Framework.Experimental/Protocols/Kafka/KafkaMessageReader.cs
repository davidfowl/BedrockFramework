#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses;
using Bedrock.Framework.Experimental.Protocols.Kafka.Services;
using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Diagnostics;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public class KafkaMessageReader : IMessageReader<KafkaResponse>
    {
        private readonly IMessageCorrelator correlations;

        public KafkaMessageReader(IMessageCorrelator messageCorrelator)
        {
            this.correlations = messageCorrelator;
        }

        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out KafkaResponse message)
        {
            var reader = new SequenceReader<byte>(input);
            message = NullResponse.Instance;

            if (!reader.TryReadBigEndian(out int messageSize))
            {
                return false;
            }

            // Let the underlying system accumulate more bytes and try again...
            if (messageSize > reader.Remaining)
            {
                return false;
            }

            if (!reader.TryReadBigEndian(out int correlationId)
                || !this.correlations.HasCorrelationId(correlationId))
            {
                return false;
            }

            var payload = reader.Sequence.Slice(reader.Position, reader.Remaining);

            var response = this.correlations.CreatedEmptyCorrelatedResponse(correlationId);

            response.FillResponse(payload);

            // extend sequencereader to remaining length so the next frame is setup correctly.
            reader.Advance(reader.Remaining);

            // Cleanup anything left over from originating request...
            if (!this.correlations.TryCompleteCorrelation(correlationId))
            {
                // TODO: Determine if this can fail...
                Debug.Assert(false);
            }

            consumed = reader.Position;
            examined = consumed;

            message = response;

            return true;
        }
    }
}
