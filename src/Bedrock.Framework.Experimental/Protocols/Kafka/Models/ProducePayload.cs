#nullable enable
#pragma warning disable CA2231 // Overload operator equals on overriding value type Equals
#pragma warning disable CA1815 // Override equals and operator equals on value types
using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public ref struct ProducePayload
    {
        public readonly TopicPartitions[] TopicPartitions;
        public readonly ReadOnlySpan<byte> Key;
        public readonly ReadOnlySpan<byte> Value;

        public ProducePayload(ref TopicPartitions topicPartitions, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            this.TopicPartitions = new[] { topicPartitions };
            this.Key = key;
            this.Value = value;
        }

        public ProducePayload(ref TopicPartitions topicPartitions, ref byte[] key, ref byte[] value)
            : this(ref topicPartitions, new ReadOnlySpan<byte>(key), new ReadOnlySpan<byte>(value))
        {
        }

        public override int GetHashCode()
        {
            // TODO: make better, length is not good approach since rented
            // buffers might be used.
            return HashCode.Combine(
                this.Key.Length,
                this.Value.Length,
                this.TopicPartitions);
        }
    }
}

#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA2231 // Overload operator equals on overriding value type Equals
