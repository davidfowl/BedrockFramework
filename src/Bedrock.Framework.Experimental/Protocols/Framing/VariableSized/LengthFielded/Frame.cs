using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Framing.VariableSized.LengthFielded
{
    public readonly struct Frame
    {
        public IHeader Header { get; }
        public ReadOnlySequence<byte> Payload { get; }

        public Frame(IHeader header, byte[] payload) : this(header, new ReadOnlySequence<byte>(payload))
        {
        }

        public Frame(IHeader header, ReadOnlySequence<byte> payload)
        {
            Header = header;
            Payload = payload;
        }
    }
}
