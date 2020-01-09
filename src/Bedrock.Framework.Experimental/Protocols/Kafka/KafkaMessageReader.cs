#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Messages;
using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests;
using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses;
using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Collections.Concurrent;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public class KafkaMessageReader : IMessageReader<KafkaResponse>
    {
        private readonly ConcurrentDictionary<int, KafkaRequest> correlations;

        public KafkaMessageReader(in ConcurrentDictionary<int, KafkaRequest> correlations)
        {
            this.correlations = correlations;
        }

        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out KafkaResponse message)
        {
            var reader = new SequenceReader<byte>(input);
            message = NullResponse.Instance;

            if (!reader.TryReadBigEndian(out int messageSize))
            {
                return false;
            }

            if (!reader.TryReadBigEndian(out int correlationId)
                || !this.correlations.ContainsKey(correlationId))
            {
                return false;
            }

            var requestType = this.correlations[correlationId];
            var payload = reader.Sequence.Slice(reader.Position, reader.Remaining);

            // extend sequencereader to remaining length so the next frame is setup correctly.
            reader.Advance(reader.Remaining);

            message = CreateResponse(requestType, payload);

            if (!this.correlations.TryRemove(correlationId, out var _))
            {
                // TODO: Determine if this can fail...
            }

            consumed = reader.Position;
            examined = consumed;

            return true;
        }

        private KafkaResponse CreateResponse(in KafkaRequest request, in ReadOnlySequence<byte> sequence)
        {
            KafkaResponse response = request switch
            {
                ApiVersionsRequest _ => new ApiVersionsResponse(),
                MetadataRequest _ => new MetadataResponse(),
                FetchRequest _ => new FetchResponse(),

                _ => throw new ArgumentException(nameof(request)),
            };

            response.ReadResponse(sequence);

            return response;
        }
    }
}

#nullable restore