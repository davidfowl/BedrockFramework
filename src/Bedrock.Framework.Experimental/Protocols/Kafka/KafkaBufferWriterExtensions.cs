#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Primitives;
using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Kafka2
{
    internal static class KafkaBufferWriterExtensions
    {
        private static ReadOnlySpan<byte> False => new byte[] { 0 };
        private static ReadOnlySpan<byte> True => new byte[] { 1 };

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
            buffer.Write(value
                ? True
                : False);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteByte<T>(ref this BufferWriter<T> buffer, byte value)
            where T : IBufferWriter<byte>
        {
            buffer.Span[0] = value;
            buffer.Advance(1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteBytes<T>(ref this BufferWriter<T> buffer, ref byte[]? bytes)
            where T : IBufferWriter<byte>
        {
            var length = bytes?.Length ?? -1;

            return WriteBytes(ref buffer, ref bytes, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int WriteBytes<T>(ref this BufferWriter<T> buffer, ref byte[]? bytes, int? length)
            where T : IBufferWriter<byte>
        {
            buffer.WriteInt32BigEndian(length ?? -1);

            if (bytes != null && length.HasValue)
            {
                var readOnlyBytes = new ReadOnlySpan<byte>(bytes, 0, length.Value);
                buffer.Write(readOnlyBytes);
            }

            return sizeof(int)
                + length ?? 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteArrayPreamble<T>(ref this BufferWriter<T> buffer, int? count)
            where T : IBufferWriter<byte>
        {
            buffer.WriteInt32BigEndian(count ?? -1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt32BigEndian<T>(ref this BufferWriter<T> buffer, int number)
            where T : IBufferWriter<byte>
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer.Span, number);
            buffer.Advance(4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteInt64BigEndian<T>(ref this BufferWriter<T> buffer, long number)
            where T : IBufferWriter<byte>
        {
            BinaryPrimitives.WriteInt64BigEndian(buffer.Span, number);
            buffer.Advance(8);
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
