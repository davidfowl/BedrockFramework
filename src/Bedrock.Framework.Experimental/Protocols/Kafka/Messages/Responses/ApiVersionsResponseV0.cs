#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses
{
    public class ApiVersionsResponseV0 : KafkaResponse
    {
        public KafkaApiKey[] SupportedApis { get; private set; } = Array.Empty<KafkaApiKey>();

        public override void FillResponse(in ReadOnlySequence<byte> response)
        {
            var reader = new SequenceReader<byte>(response);

            var error = reader.ReadErrorCode();

            this.ThrowIfError(error);

            if (!reader.TryReadBigEndian(out int arraySize)
                && arraySize != -1)
            {
                return;
            }

            var values = new KafkaApiKey[arraySize];

            for (int i = 0; i < arraySize; i++)
            {
                short kafkaApiKey = reader.ReadInt16BigEndian();
                short min = reader.ReadInt16BigEndian();
                short max = reader.ReadInt16BigEndian();

                values[i] = new KafkaApiKey((KafkaApiKeys)kafkaApiKey, min, max);
            }

            this.SupportedApis = values;
        }
    }
}
