using Bedrock.Framework.Infrastructure;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests
{
    public class ApiVersionsRequest : KafkaRequest
    {
        public ApiVersionsRequest()
            : base(KafkaApiKeys.ApiVersions, apiVersion: 2)
        {
        }

        public override void WriteRequest(ref BufferWriter<IBufferWriter<byte>> output)
        {
        }
    }
}
