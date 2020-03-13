using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods
{
    public class ConnectionOpen : MethodBase, IAmqpMessage
    {
        public override byte ClassId => 10;
        public override byte MethodId => 40;

        public ReadOnlyMemory<byte> Vhost { get; private set; }
        public ReadOnlyMemory<byte> Reserved1 { get; private set; }

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
            int PayloadLength = 1+Vhost.Length + 1+Reserved1.Length + MethodHeaderLength + sizeof(byte);
            var buffer = output.GetSpan(RabbitMQMessageFormatter.HeaderLength + PayloadLength + 1);

            WriteHeader(ref buffer, 0, PayloadLength);

            BinaryPrimitives.WriteUInt16BigEndian(buffer, ClassId);            
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(2), MethodId); 
            buffer[4] = (byte)Vhost.Length;           
            Vhost.Span.CopyTo(buffer.Slice(5)); 
            buffer[5+Vhost.Length] = (byte)Reserved1.Length;            
            Reserved1.Span.CopyTo(buffer.Slice(5 + Vhost.Length + 1));
            buffer[5 + Vhost.Length + 1 + Reserved1.Length] = Reserved2; 
            buffer[PayloadLength] = (byte)FrameType.End;

            output.Advance(RabbitMQMessageFormatter.HeaderLength + PayloadLength + sizeof(byte));
        }
    }
}
