#nullable enable
#pragma warning disable CA2231 // Overload operator equals on overriding value type Equals
#pragma warning disable CA1815 // Override equals and operator equals on value types
using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public ref struct ProducePayload
    {
        public readonly TopicPartition TopicPartition;
        public readonly ReadOnlySpan<byte> Key;
        public readonly ReadOnlySpan<byte> Value;

        public ProducePayload(ref TopicPartition topicPartition, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            this.TopicPartition = topicPartition;
            this.Key = key;
            this.Value = value;
        }

        public ProducePayload(ref TopicPartition topicPartition, ref byte[] key, ref byte[] value)
            : this(ref topicPartition, new ReadOnlySpan<byte>(key), new ReadOnlySpan<byte>(value))
        {
        }

        public override int GetHashCode()
        {
            // TODO: make better, length is not good approach since rented
            // buffers might be used.
            return HashCode.Combine(
                this.Key.Length,
                this.Value.Length,
                this.TopicPartition);
        }
    }
}

#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA2231 // Overload operator equals on overriding value type Equals
