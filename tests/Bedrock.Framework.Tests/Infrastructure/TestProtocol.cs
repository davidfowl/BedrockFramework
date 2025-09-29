using System;
using System.Buffers;
using Bedrock.Framework.Protocols;

namespace Bedrock.Framework.Experimental.Tests.Infrastructure
{
    internal sealed class TestProtocol : IMessageReader<byte[]>, IMessageWriter<byte[]>
    {
        private readonly int _messageLength;

        public TestProtocol(int messageLength)
            => _messageLength = messageLength;

        public TestProtocol() : this(0)
        {
        }

        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out byte[] message)
        {
            if (input.Length < _messageLength)
            {
                message = Array.Empty<byte>();
                return false;
            }

            var buffer = input.Slice(0, _messageLength);
            message = buffer.ToArray();
            consumed = buffer.End;
            examined = buffer.End;

            return true;
        }

        public void WriteMessage(byte[] message, IBufferWriter<byte> output) => output.Write(message);
    }
}