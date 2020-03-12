using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp.Methods
{
    public abstract class MethodBase
    {        
        public abstract byte ClassId { get; }
        public abstract byte MethodId { get; }

        public const int MethodHeaderLength = 4;

        public void WriteHeader(ref Span<byte> buffer, ushort channel, int payloadLength)
        {
            buffer[0] = (byte)FrameType.Method;
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(1), channel);
            BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(3), payloadLength);
            buffer = buffer.Slice(7);
        }
    }
}
