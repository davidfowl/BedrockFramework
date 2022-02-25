using System;

namespace Bedrock.Framework.Experimental.Protocols.Framing.VariableSized.LengthFielded
{
    public interface IHeader
    {
        public int PayloadLength { get; }

        public ReadOnlySpan<byte> AsSpan();
    }
}
