using Bedrock.Framework.Protocols;
using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Framing.VariableSizeLengthFielded
{
    public class VariableSizeLengthFieldedProtocol : IMessageReader<Frame>, IMessageWriter<Frame>
    {
        private readonly int _headerLength;
        private readonly Func<ReadOnlySequence<byte>, IHeader> _createHeader;

        private IHeader? _header;

        public VariableSizeLengthFieldedProtocol(int headerLength, Func<ReadOnlySequence<byte>, IHeader> createHeader)
        {
            if (headerLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(headerLength), "Header length must be greater than 0.");
            }

            _headerLength = headerLength;
            _createHeader = createHeader ?? throw new ArgumentNullException(nameof(createHeader));
        }

        #region IMessageReader
        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out Frame message)
        {
            // Header
            if (_header == null)
            {
                if (!TryParseHeader(input, out var headerSequence))
                {
                    message = default;
                    return false;
                }

                _header = _createHeader(headerSequence);
            }

            if (!TryParsePayload(input, out var payloadSequence))
            {
                message = default;
                return false;
            }

            consumed = payloadSequence.End;
            examined = consumed;
            message = new Frame(_header, payloadSequence);
            _header = null;
            return true;
        }

        private bool TryParseHeader(in ReadOnlySequence<byte> input, out ReadOnlySequence<byte> headerSequence)
        {
            if (input.Length < _headerLength)
            {
                headerSequence = default;
                return false;
            }

            headerSequence = input.Slice(0, _headerLength);
            return true;
        }

        private bool TryParsePayload(in ReadOnlySequence<byte> input, out ReadOnlySequence<byte> payloadSequence)
        {
            int messageLength = _headerLength + _header.PayloadLength;
            if (input.Length < messageLength)
            {
                payloadSequence = default;
                return false;
            }

            payloadSequence = input.Slice(_headerLength, _header.PayloadLength);
            return true;
        }
        #endregion

        #region IMessageWriter
        public void WriteMessage(Frame message, IBufferWriter<byte> output)
        {
            // Header
            output.Write(message.Header.AsSpan());

            // Payload
            foreach (var payloadSegment in message.Payload)
            {
                output.Write(payloadSegment.Span);
            }
        }
        #endregion
    }
}
