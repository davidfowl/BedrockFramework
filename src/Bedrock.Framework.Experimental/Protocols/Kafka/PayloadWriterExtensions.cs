#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests;
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
    internal static class PayloadrWriterExtensions
    {
        private static ReadOnlySpan<byte> False => new byte[] { (byte)0 };
        private static ReadOnlySpan<byte> True => new byte[] { (byte)1 };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PayloadWriter WriteString(ref this PayloadWriter writer, string value)
        {
            // buffer.WriteInt16BigEndian((short)value.Length);
            // buffer.Write(Encoding.UTF8.GetBytes(value));

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PayloadWriter WriteInt32BigEndian(ref this PayloadWriter writer, int number)
        {
            // BinaryPrimitives.WriteInt32BigEndian(buffer.Span, number);
            // buffer.Advance(4);

            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PayloadWriter WriteInt16BigEndian(ref this PayloadWriter writer, short number)
        {
            // BinaryPrimitives.WriteInt16BigEndian(buffer.Span, number);
            // buffer.Advance(2);

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
    }
}
