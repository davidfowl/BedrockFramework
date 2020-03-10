using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp.Methods
{
    public class ConnectionOk : MethodBase, IAmqpMessage
    {
        private readonly ReadOnlyMemory<byte> _securityMechanism;
        private readonly ReadOnlyMemory<byte> _credentials;
        private readonly ReadOnlyMemory<byte> _locale;

        public override byte ClassId => 10;
        public override byte MethodId => 11;

        public ConnectionOk(ReadOnlyMemory<byte> securityMechanism, ReadOnlyMemory<byte> credentials, ReadOnlyMemory<byte> locale)
        {
            _securityMechanism = securityMechanism;
            _credentials = credentials;
            _locale = locale;
        }

        public bool TryParse(ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            throw new NotImplementedException();
        }

        public void Write(IBufferWriter<byte> output)
        {
            int PayloadLength = _securityMechanism.Length + _credentials.Length + _locale.Length + 14;
            var buffer = output.GetSpan(AmqpMessageFormatter.HeaderLength + PayloadLength + 1);

            WriteHeader(ref buffer, 0, PayloadLength);
            BinaryPrimitives.WriteUInt16BigEndian(buffer, ClassId);
            buffer = buffer.Slice(2);
            BinaryPrimitives.WriteUInt16BigEndian(buffer, MethodId);
            buffer = buffer.Slice(2);
            BinaryPrimitives.WriteUInt32BigEndian(buffer, 0);
            buffer = buffer.Slice(4);
            buffer[0] = (byte)_securityMechanism.Length;
            buffer = buffer.Slice(1);
            _securityMechanism.Span.CopyTo(buffer);
            buffer = buffer.Slice(_securityMechanism.Length);
            BinaryPrimitives.WriteInt32BigEndian(buffer, _credentials.Length);
            buffer = buffer.Slice(4);
            _credentials.Span.CopyTo(buffer);
            buffer = buffer.Slice(_credentials.Length);
            buffer[0] = (byte)_locale.Length;
            buffer = buffer.Slice(1);
            _locale.Span.CopyTo(buffer);
            buffer = buffer.Slice(_locale.Length);
            buffer[0] = (byte)FrameType.End;
            output.Advance(AmqpMessageFormatter.HeaderLength + PayloadLength + sizeof(byte));
        }
    }
}
