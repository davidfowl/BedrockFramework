#nullable enable

using System;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Primitives
{
    public readonly struct NullableString
    {
        public readonly short Length;
        public readonly short Size;
        public readonly ReadOnlyMemory<byte> Bytes;

        public NullableString(string? value)
        {
            Size = sizeof(short);
            if (value is null)
            {
                Length = -1;
                Bytes = Memory<byte>.Empty;
            }
            else
            {
                Bytes = Encoding.UTF8.GetBytes(value);
                Length = (short)Bytes.Length;
                Size += Length;
            }
        }
    }
}

#nullable restore