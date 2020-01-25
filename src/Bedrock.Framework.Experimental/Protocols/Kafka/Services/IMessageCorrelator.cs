#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests;
using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses;
using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Services
{
    public interface IMessageCorrelator : IDisposable
    {
        bool HasCorrelationId(in int correlationId);
        int GetCorrelationId(in KafkaRequest request);

        bool TryAdd(in int correlationId, in KafkaRequest kafkaRequest);
        bool TryCompleteCorrelation(in int correlationId);

        KafkaResponse CreatedEmptyCorrelatedResponse(in KafkaRequest request);
        KafkaResponse CreatedEmptyCorrelatedResponse(in int correlationId);
    }
}
