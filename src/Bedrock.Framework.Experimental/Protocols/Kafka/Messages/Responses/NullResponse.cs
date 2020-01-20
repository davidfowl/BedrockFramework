using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses
{
    internal class NullResponse : KafkaResponse
    {
        internal static NullResponse Instance = new NullResponse();

        public override void FillResponse(in ReadOnlySequence<byte> response)
        {
        }
    }
}
