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

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is NullableString))
            {
                return false;
            }

            var that = (NullableString)obj;

            return this.Length.Equals(that.Length)
                && this.Size.Equals(that.Size)
                && this.Bytes.Equals(that.Bytes);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.Length,
                this.Size,
                this.Bytes);
        }

        public static bool operator ==(NullableString left, NullableString right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NullableString left, NullableString right)
        {
            return !(left == right);
        }
    }
}
