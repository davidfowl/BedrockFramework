using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses
{
    public class ApiVersionsResponse : KafkaResponse
    {
        public IEnumerable<KafkaApiKey> SupportedApis { get; private set; } = Enumerable.Empty<KafkaApiKey>();
        public int ThrottleTimeMs { get; private set; }

        public override void ReadResponse(in ReadOnlySequence<byte> response)
        {
            var reader = new SequenceReader<byte>(response);

            var error = reader.ReadErrorCode();

            ThrowIfError(error);

            if (!reader.TryReadBigEndian(out int arraySize)
                && arraySize != -1)
            {
                return;
            }

            var values = new List<KafkaApiKey>(arraySize);

            for (int i = 0; i < arraySize; i++)
            {
                short kafkaApiKey = reader.ReadInt16BigEndian();
                short min = reader.ReadInt16BigEndian();
                short max = reader.ReadInt16BigEndian();

                values.Add(new KafkaApiKey((KafkaApiKeys)kafkaApiKey, min, max));
            }

            this.SupportedApis = values;

            this.ThrottleTimeMs = reader.ReadInt32BigEndian();
        }
    }
}
