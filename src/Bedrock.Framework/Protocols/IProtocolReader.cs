using System;
using System.Buffers;

namespace Bedrock.Framework.Protocols
{
    public interface IProtocolReader<TMessage>
    {
        bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out TMessage message);
    }
}
