using System;
using System.Buffers;

namespace ServerApplication.Framing.VariableSizeLengthFielded
{
    internal class HeaderFactory
    {
        public Header CreateHeader(int payloadLength, int someCustomData) => new Header(payloadLength, someCustomData);

        public Header CreateHeader(in ReadOnlySequence<byte> headerSequence)
        {
            if (headerSequence.IsSingleSegment)
            {
                return CreateHeader(headerSequence.FirstSpan);
            }
            else
            {
                Span<byte> headerData = stackalloc byte[Helper.HeaderLength];
                headerSequence.CopyTo(headerData);
                return CreateHeader(headerData);
            }
        }

        public Header CreateHeader(in ReadOnlySpan<byte> headerData) => new(headerData);
    }
}
