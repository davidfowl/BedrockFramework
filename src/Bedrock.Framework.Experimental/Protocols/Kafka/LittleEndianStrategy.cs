#nullable enable
#pragma warning disable CA1815 // Override equals and operator equals on value types

using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public class LittleEndianStrategy : IPayloadWriterStrategy
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteByte(Span<byte> destination, byte value)
        {
            destination[0] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt16(Span<byte> destination, short value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(destination, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt32(Span<byte> destination, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteInt64(Span<byte> destination, long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(destination, value);
        }
    }
}

#pragma warning restore CA1815 // Override equals and operator equals on value types
