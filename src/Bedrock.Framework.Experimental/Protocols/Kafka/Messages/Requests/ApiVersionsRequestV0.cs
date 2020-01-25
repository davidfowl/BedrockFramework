using Bedrock.Framework.Experimental.Protocols.Kafka.Models;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests
{
    public class ApiVersionsRequestV0 : KafkaRequest
    {
        public static ApiVersionsRequestV0 AllSupportedApis = new ApiVersionsRequestV0();

        public ApiVersionsRequestV0()
            : base(KafkaApiKeys.ApiVersions, apiVersion: 0)
        {
        }

        public override void WriteRequest(ref PayloadWriter writer)
        {
        }
    }
}
