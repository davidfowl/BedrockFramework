#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Primitives;
using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    internal static class KafkaBufferWriterExtensions
    {
        private static ReadOnlySpan<byte> False => new byte[] { (byte)0 };
        private static ReadOnlySpan<byte> True => new byte[] { (byte)1 };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteString<T>(ref this BufferWriter<T> buffer, string value)
            where T : IBufferWriter<byte>
        {
            buffer.WriteInt16BigEndian((short)value.Length);
            buffer.Write(Encoding.UTF8.GetBytes(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteBoolean<T>(ref this BufferWriter<T> buffer, bool value)
            where T : IBufferWriter<byte>
        {
            if (value)
            {
                buffer.Write(True);
            }
            else
            {
                buffer.Write(False);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteArrayPreamble<T>(ref this BufferWriter<T> buffer, int? count)
            where T : IBufferWriter<byte>
        {
            if (count.HasValue)
            {
                buffer.WriteInt32BigEndian(count.Value);
            }
            else
            {
                buffer.WriteInt32BigEndian(-1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt32BigEndian<T>(ref this BufferWriter<T> buffer, int number)
            where T : IBufferWriter<byte>
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer.Span, number);
            buffer.Advance(4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt16BigEndian<T>(ref this BufferWriter<T> buffer, short number)
            where T : IBufferWriter<byte>
        {
            BinaryPrimitives.WriteInt16BigEndian(buffer.Span, number);
            buffer.Advance(2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteNullableString<T>(ref this BufferWriter<T> buffer, ref NullableString value)
            where T : IBufferWriter<byte>
        {
            buffer.WriteInt16BigEndian(value.Length);

            if (value.Length != -1)
            {
                buffer.Write(value.Bytes.Span);
            }
        }
    }
}

#nullable restore