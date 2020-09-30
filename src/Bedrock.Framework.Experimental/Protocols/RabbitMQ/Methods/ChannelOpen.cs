using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods
{
    public class ChannelOpen : MethodBase, IAmqpMessage
    {
        public override byte ClassId => 20;
        public override byte MethodId => 10;       

        public ReadOnlyMemory<byte> Reserved1 { get; }
        public ushort Channel { get; private set; }

        public ChannelOpen(ReadOnlyMemory<byte> reserved1, ushort channel)
        {
            Reserved1 = reserved1;
            Channel = channel;
        }

        public bool TryParse(in ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            throw new NotImplementedException();
        }

        public void Write(IBufferWriter<byte> output)
        {  
            var payloadLength = 1 + Reserved1.Length + MethodHeaderLength;
            var buffer = output.GetSpan(RabbitMQMessageFormatter.HeaderLength + payloadLength + 1);

            WriteHeader(ref buffer, Channel, payloadLength);

            BinaryPrimitives.WriteUInt16BigEndian(buffer, ClassId);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(2), MethodId);
            buffer[4] = (byte)Reserved1.Length;
            Reserved1.Span.CopyTo(buffer.Slice(5));
            buffer[payloadLength]= (byte)FrameType.End;

            output.Advance(RabbitMQMessageFormatter.HeaderLength + payloadLength + sizeof(byte));
        }
    }
}
