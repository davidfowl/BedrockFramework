#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Primitives;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    internal static class PayloadWriterExtensions
    {
        private static ReadOnlySpan<byte> False => new byte[] { (byte)0 };
        private static ReadOnlySpan<byte> True => new byte[] { (byte)1 };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PayloadWriter WriteString(this PayloadWriter writer, string? value)
        {
            var length = value?.Length ?? -1;
            writer.Write((short)length);

            if (value != null)
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                writer.WriteBytes(ref bytes!);
            }

            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PayloadWriter WriteBytes(ref this PayloadWriter writer, ref byte[]? bytes)
        {
            var length = bytes?.Length ?? -1;

            return writer.WriteBytes(ref bytes, length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PayloadWriter WriteBytes(ref this PayloadWriter writer, ref ReadOnlySpan<byte> bytes)
        {
            writer.ToParent.Write(bytes);

            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PayloadWriter WriteBytes(ref this PayloadWriter writer, ref byte[]? bytes, int? length)
        {
            writer.Write(length ?? -1);

            if (bytes != null && length.HasValue)
            {
                var readOnlyBytes = new ReadOnlySpan<byte>(bytes, 0, length.Value);
                writer.WriteBytes(ref readOnlyBytes);
            }

            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PayloadWriter WriteBoolean(ref this PayloadWriter writer, bool value)
        {
            if (value)
            {
                //buffer.Write(True);
            }
            else
            {
                //buffer.Write(False);
            }

            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PayloadWriter WriteArrayPreamble(ref this PayloadWriter writer, int? count)
        {
            if (count.HasValue)
            {
                // buffer.WriteInt32BigEndian(count.Value);
            }
            else
            {
                // buffer.WriteInt32BigEndian(-1);
            }

            return writer;
        }

        internal static PayloadWriter WriteArray<T>(this PayloadWriter writer, T[] array, Action<T, PayloadWriterContext> action)
        {
            writer.Write(array.Length);

            for (int i = 0; i < array.Length; i++)
            {
                action(array[i], writer.Settings);
            }

            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PayloadWriter WriteNullableString(ref this PayloadWriter writer, ref NullableString value)
        {
            // buffer.WriteInt16BigEndian(value.Length);

            if (value.Length != -1)
            {
                // buffer.Write(value.Bytes.Span);
            }

            return writer;
        }

        internal static PayloadWriter StartCrc32Calculation(ref this PayloadWriter writer)
        {
            return writer;
        }

        internal static PayloadWriter EndCrc32Calculation(ref this PayloadWriter writer)
        {
            return writer;
        }
    }
}
