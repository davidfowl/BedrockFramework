using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public interface IPayloadWriterStrategy
    {
        void WriteInt64(Span<byte> destination, long value);
        void WriteInt32(Span<byte> destination, int value);
        void WriteInt16(Span<byte> destination, short value);
        void WriteByte(Span<byte> destination, byte value);
    }
}