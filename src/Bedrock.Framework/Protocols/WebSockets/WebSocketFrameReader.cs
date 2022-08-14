using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// An implementation of IMessageReader that parses WebSocket message frames.
    /// </summary>
    public class WebSocketFrameReader : IMessageReader<WebSocketReadFrame>
    {
        /// <summary>
        /// An instance of the WebSocketFrameReader.
        /// </summary>
        private WebSocketPayloadReader _payloadReader;

        /// <summary>
        /// Attempts to parse a message from a sequence.
        /// </summary>
        /// <param name="input">The sequence to parse messages from.</param>
        /// <param name="consumed">The position in the sequence to which the parser has fully consumed.</param>
        /// <param name="examined">The position in the sequence to which the parser has examined but not yet consumed.</param>
        /// <param name="message">The WebSocketHeader for the current message frame.</param>
        /// <returns>True if parsed successfully, false otherwise.</returns>
        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out WebSocketReadFrame message)
        {
            //We need to at least be able to read the start of frame header
            if (input.Length < 2)
            {
                message = default;
                return false;
            }

            if (input.IsSingleSegment || input.FirstSpan.Length >= 14)
            {
                if (TryParseSpan(input.FirstSpan, input.Length, out var bytesRead, out message))
                {
                    consumed = input.GetPosition(bytesRead);
                    examined = consumed;

                    return true;
                }

                return false;

            }
            else
            {
                Span<byte> tempSpan = stackalloc byte[14];

                var bytesToCopy = Math.Min(input.Length, tempSpan.Length);
                input.Slice(0, bytesToCopy).CopyTo(tempSpan);

                if (TryParseSpan(tempSpan, input.Length, out var bytesRead, out message))
                {
                    consumed = input.GetPosition(bytesRead);
                    examined = consumed;

                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Attempts to parse a span for a WebSocket frame header.
        /// </summary>
        /// <param name="span">The span to attempt to parse.</param>
        /// <param name="inputLength">The input sequence length.</param>
        /// <param name="bytesRead">The number of bytes read from the span.</param>
        /// <param name="message">The WebSocketReadFrame read from the span.</param>
        /// <returns>True if the span could be parsed, false otherwise.</returns>
        private bool TryParseSpan(in ReadOnlySpan<byte> span, long inputLength, out int bytesRead, out WebSocketReadFrame message)
        {
            bytesRead = 0;

            var finOpcodeByte = span[0];
            var maskLengthByte = span[1];

            var masked = (maskLengthByte & 0b1000_0000) != 0;
            ulong initialPayloadLength = (ulong)(maskLengthByte & 0b0111_1111);

            var maskSize = masked ? 4 : 0;
            var extendedPayloadLengthSize = 0;

            switch (initialPayloadLength)
            {
                case 126:
                    extendedPayloadLengthSize = 2;
                    break;
                case 127:
                    extendedPayloadLengthSize = 8;
                    break;
            }

            if (inputLength < extendedPayloadLengthSize + maskSize + 2)
            {
                message = default;
                return false;
            }

            var fin = (finOpcodeByte & 0b1000_0000) != 0;
            var opcode = (WebSocketOpcode)(finOpcodeByte & 0b0000_1111);

            ulong payloadLength = 0;
            if (extendedPayloadLengthSize == 2)
            {
                payloadLength = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(2));
            }
            else if (extendedPayloadLengthSize == 8)
            {
                payloadLength = BinaryPrimitives.ReadUInt64BigEndian(span.Slice(2));
            }
            else
            {
                payloadLength = initialPayloadLength;
            }

            int maskingKey = 0;
            if (masked)
            {
                maskingKey = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(2 + extendedPayloadLengthSize));
            }

            var header = new WebSocketHeader(fin, opcode, masked, payloadLength, maskingKey);

            if(_payloadReader == null)
            {
                _payloadReader = new WebSocketPayloadReader(header);
            }
            else
            {
                _payloadReader.Reset(header);
            }

            message = new WebSocketReadFrame(header, _payloadReader);
            bytesRead = 2 + extendedPayloadLengthSize + maskSize;
            return true;
        }
    }
}
