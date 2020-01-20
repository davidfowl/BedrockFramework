using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using Bedrock.Framework.Infrastructure;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests
{
    public class ApiVersionsRequestV0 : KafkaRequest
    {
        public static ApiVersionsRequestV0 AllSupportedApis = new ApiVersionsRequestV0();

        public ApiVersionsRequestV0()
            : base(KafkaApiKeys.ApiVersions, apiVersion: 0)
        {
        }

        public override int GetPayloadSize()
            => 0;

        public override void WriteRequest(ref BufferWriter<IBufferWriter<byte>> output)
        {
        }
    }
}
