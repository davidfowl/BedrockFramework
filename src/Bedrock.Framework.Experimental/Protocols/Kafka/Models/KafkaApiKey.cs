using Bedrock.Framework.Experimental.Protocols.Kafka.Messages;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct KafkaApiKey
    {
        public readonly KafkaApiKeys ApiKey;
        public readonly short MinimumVersion;
        public readonly short MaximumVersion;

        public KafkaApiKey(KafkaApiKeys apiKey, short minVer, short maxVer)
        {
            this.ApiKey = apiKey;
            this.MinimumVersion = minVer;
            this.MaximumVersion = maxVer;
        }

        public override string ToString()
            => $"{this.ApiKey} - {this.MinimumVersion}:{this.MaximumVersion}";
    }
}
