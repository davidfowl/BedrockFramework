using System;
using System.Buffers;
using System.Buffers.Binary;
using Bedrock.Framework.Protocols;

namespace Protocols
{
    public class LengthPrefixedProtocol : IProtocolReader<Message>, IProtocolWriter<Message>
    {
        public bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out Message message)
        {
            var reader = new SequenceReader<byte>(input);
            if (!reader.TryReadBigEndian(out int length) || input.Length < length)
            {
                consumed = input.Start;
                examined = input.End;
                message = default;
                return false;
            }

            var payload = input.Slice(reader.Position, length);
            message = new Message(payload);

            consumed = payload.End;
            examined = consumed;
            return true;
        }

        public void WriteMessage(Message message, IBufferWriter<byte> output)
        {
            var lengthBuffer = output.GetSpan(4);
            BinaryPrimitives.WriteInt32BigEndian(lengthBuffer, (int)message.Payload.Length);
            output.Advance(4);
            foreach (var memory in message.Payload)
            {
                output.Write(memory.Span);
            }
        }
    }

    public struct Message
    {
        public Message(byte[] payload) : this(new ReadOnlySequence<byte>(payload))
        {

        }

        public Message(ReadOnlySequence<byte> payload)
        {
            Payload = payload;
        }

        public ReadOnlySequence<byte> Payload { get; }
    }
}
