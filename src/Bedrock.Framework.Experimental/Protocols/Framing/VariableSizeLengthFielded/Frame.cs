using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Framing.VariableSizeLengthFielded
{
    public readonly struct Frame
    {
        public readonly IHeader Header { get; }
        public readonly ReadOnlySequence<byte> Payload { get; }

        public Frame(IHeader header, byte[] payload) : this(header, new ReadOnlySequence<byte>(payload))
        {
        }
        
        public Frame(IHeader header, ReadOnlyMemory<byte> payload) : this(header, new ReadOnlySequence<byte>(payload))
        {
        }

        public Frame(IHeader header, ReadOnlySequence<byte> payload)
        {
            Header = header;
            Payload = payload;
        }
    }
}
