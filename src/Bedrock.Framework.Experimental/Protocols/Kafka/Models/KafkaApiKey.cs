#nullable enable

using System;

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

        public override bool Equals(object obj)
        {
            if(obj == null || !(obj is KafkaApiKey))
            {
                return false;
            }

            var that = (KafkaApiKey)obj;

            return this.ApiKey.Equals(that.ApiKey)
                && this.MaximumVersion.Equals(that.MaximumVersion)
                && this.MinimumVersion.Equals(that.MinimumVersion);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.ApiKey,
                this.MaximumVersion,
                this.MinimumVersion);
        }

        public static bool operator ==(KafkaApiKey left, KafkaApiKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KafkaApiKey left, KafkaApiKey right)
        {
            return !(left == right);
        }
    }
}
