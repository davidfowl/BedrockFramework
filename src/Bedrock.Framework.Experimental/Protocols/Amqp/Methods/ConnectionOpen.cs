using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp.Methods
{
    public class ConnectionOpen : MethodBase, IAmqpMessage
    {
        public override byte ClassId => 10;
        public override byte MethodId => 40;

        public ReadOnlyMemory<byte> Vhost { get; }
        public ReadOnlyMemory<byte> Reserved1 { get; }
        public byte Reserved2 { get; }

        public ConnectionOpen(ReadOnlyMemory<byte> vhost, ReadOnlyMemory<byte> reserved1, byte reserved2)
        {
            Vhost = vhost;
            Reserved1 = reserved1;
            Reserved2 = reserved2;
        }

        public bool TryParse(ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            throw new NotImplementedException();
        }

        public void Write(IBufferWriter<byte> output)
        {
            int PayloadLength = Vhost.Length + Reserved1.Length + sizeof(byte);
            var buffer = output.GetSpan(AmqpMessageFormatter.HeaderLength + PayloadLength + 1);

            WriteHeader(ref buffer, 0, PayloadLength);
            BinaryPrimitives.WriteUInt16BigEndian(buffer, ClassId);
            buffer = buffer.Slice(2);
            BinaryPrimitives.WriteUInt16BigEndian(buffer, MethodId);
            buffer = buffer.Slice(2);            
           
            buffer[0] = (byte)Vhost.Length;
            buffer = buffer.Slice(1);
            Vhost.Span.CopyTo(buffer);
            buffer = buffer.Slice(Vhost.Length);

            buffer[0] = (byte)Reserved1.Length;
            buffer = buffer.Slice(1);
            Reserved1.Span.CopyTo(buffer);
            buffer = buffer.Slice(Reserved1.Length);

            buffer[0] = Reserved2;
            buffer = buffer.Slice(1);
           
            buffer[0] = (byte)FrameType.End;
            output.Advance(AmqpMessageFormatter.HeaderLength + PayloadLength + sizeof(byte));
        }
    }
}
