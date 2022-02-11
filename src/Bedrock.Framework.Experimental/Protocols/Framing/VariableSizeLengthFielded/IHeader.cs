using System;

namespace Bedrock.Framework.Experimental.Protocols.Framing.VariableSizeLengthFielded
{
    public interface IHeader
    {
        public int PayloadLength { get; }

        public ReadOnlySpan<byte> AsSpan();
    }
}
