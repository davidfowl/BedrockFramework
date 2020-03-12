using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp.Methods
{
    public class ConnectionOk : MethodBase, IAmqpMessage
    {
        public ReadOnlyMemory<byte> SecurityMechanism { get; private set; }
        public ReadOnlyMemory<byte> Credentials { get; private set; }
        public ReadOnlyMemory<byte> Locale { get; private set; }

        public override byte ClassId => 10;
        public override byte MethodId => 11;

        public ConnectionOk(ReadOnlyMemory<byte> securityMechanism, ReadOnlyMemory<byte> credentials, ReadOnlyMemory<byte> locale)
        {
            SecurityMechanism = securityMechanism;
            Credentials = credentials;
            Locale = locale;
        }

        public bool TryParse(ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            throw new NotImplementedException();
        }

        public void Write(IBufferWriter<byte> output)
        {
            int PayloadLength = SecurityMechanism.Length + Credentials.Length + Locale.Length + 14;
            var buffer = output.GetSpan(AmqpMessageFormatter.HeaderLength + PayloadLength + 1);

            WriteHeader(ref buffer, 0, PayloadLength);

            BinaryPrimitives.WriteUInt16BigEndian(buffer, ClassId);            
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(2), MethodId);           
            //TO DO replace by client properties
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(4), 0);
            buffer = buffer.Slice(8);
            buffer[0] = (byte)SecurityMechanism.Length;           
            SecurityMechanism.Span.CopyTo(buffer.Slice(1));
            buffer = buffer.Slice(SecurityMechanism.Length+1);
            BinaryPrimitives.WriteInt32BigEndian(buffer, Credentials.Length);
            buffer = buffer.Slice(4);
            Credentials.Span.CopyTo(buffer);
            buffer = buffer.Slice(Credentials.Length);
            buffer[0] = (byte)Locale.Length;
            buffer = buffer.Slice(1);
            Locale.Span.CopyTo(buffer);
            buffer = buffer.Slice(Locale.Length);
            buffer[0] = (byte)FrameType.End;

            output.Advance(AmqpMessageFormatter.HeaderLength + PayloadLength + sizeof(byte));
        }
    }
}
