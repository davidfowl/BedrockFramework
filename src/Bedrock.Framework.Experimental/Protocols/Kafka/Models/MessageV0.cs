#pragma warning disable CA1815 // Override equals and operator equals on value types
#nullable enable

using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly ref struct MessageV0
    {
        public readonly byte Magic;
        public readonly byte Attributes;

        public readonly int? KeyLength;
        public readonly byte[]? Key;

        public readonly int? ValueLength;
        public readonly byte[]? Value;

        public MessageV0(
            byte magic,
            byte attributes,
            int? keyLength,
            byte[]? key,
            int? valueLength,
            byte[]? value)
        {
            this.Magic = magic;
            this.Attributes = attributes;

            this.KeyLength = keyLength;
            this.Key = key;

            this.ValueLength = valueLength;
            this.Value = value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.Magic,
                this.Attributes,
                this.KeyLength,
                this.Key,
                this.ValueLength,
                this.Value);
        }

        public void WritePayload(ref PayloadWriterContext settings)
        {
            settings.CreatePayloadWriter()
                .StartCalculatingSize("message")
                //.StartCrc32Calculation()
                    .Write(this.Magic)
                    .Write(this.Attributes)
                    .Write(this.Key, this.KeyLength)
                    .Write(this.Value, this.ValueLength)
                //.EndCrc32Calculation()
                .EndSizeCalculation("message");
        }
    }
}

#pragma warning restore CA1815 // Override equals and operator equals on value types