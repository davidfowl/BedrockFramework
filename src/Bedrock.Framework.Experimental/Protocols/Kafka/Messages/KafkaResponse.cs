using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages
{
    public abstract class KafkaResponse
    {
        public virtual void ThrowIfError(KafkaErrorCode errorCode)
        {
            if (errorCode != KafkaErrorCode.NONE)
            {
                throw new InvalidOperationException($"Error Code Received: {errorCode}");
            }
        }

        public abstract void ReadResponse(in ReadOnlySequence<byte> response);
    }
}
