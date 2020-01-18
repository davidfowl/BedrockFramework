using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    /// <summary>
    /// An implementation of IMessageReader that parses WebSocket message frames.
    /// </summary>
    public struct WebSocketFrameReader : IMessageReader<WebSocketReadFrame>
    {
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
            var reader = new SequenceReader<byte>(input);

            //We need to at least be able to read the start of frame header
            if (input.Length < 2)
            {
                message = default;
                return false;
            }

            reader.TryRead(out var finOpcodeByte);
            reader.TryRead(out var maskLengthByte);

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

            if (reader.Remaining < extendedPayloadLengthSize + maskSize)
            {
                message = default;
                return false;
            }
            
            var fin = (finOpcodeByte & 0b1000_0000) != 0;
            var opcode = (WebSocketOpcode)(finOpcodeByte & 0b0000_1111);

            ulong payloadLength = 0;
            if (extendedPayloadLengthSize == 2)
            {
                short length;

                reader.TryReadBigEndian(out length);
                payloadLength = (ulong)length;
            }
            else if (extendedPayloadLengthSize == 8)
            {
                long length;

                reader.TryReadBigEndian(out length);
                payloadLength = (ulong)length;
            }
            else
            {
                payloadLength = initialPayloadLength;
            }

            int maskingKey = 0;
            if (masked)
            {
                reader.TryReadBigEndian(out maskingKey);
            }

            var header = new WebSocketHeader(fin, opcode, masked, payloadLength, maskingKey);
            message = new WebSocketReadFrame(header, new WebSocketPayloadReader(header));

            consumed = input.GetPosition(2 + extendedPayloadLengthSize + maskSize);
            examined = consumed;
            return true;
        }
    }
}
