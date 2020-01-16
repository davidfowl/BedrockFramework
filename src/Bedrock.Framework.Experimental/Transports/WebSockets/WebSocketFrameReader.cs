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
        /// <param name="examined">The position in the sequence to which the parse has examined but not yet consumed.</param>
        /// <param name="message">The WebSocketHeader for the current message frame.</param>
        /// <returns>True if parsed successfully, false otherwise.</returns>
        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out WebSocketReadFrame message)
        {
            var reader = new SequenceReader<byte>(input);

            //We need to at least be able to read the start of frame header
            if (input.Length < 2)
            {
                consumed = input.Start;
                examined = input.End;
                message = default;
                return false;
            }

            reader.TryRead(out var finOpcodeByte);
            reader.TryRead(out var maskLengthByte);

            var masked = (maskLengthByte & 0b1000_0000) != 0;
            ulong payloadLength = (ulong)(maskLengthByte & 0b0111_1111);

            var maskSize = masked ? 0 : 4;
            var extendedPayloadLengthSize = 0;

            switch (payloadLength)
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
                consumed = input.Start;
                examined = input.End;
                message = default;
                return false;
            }

            var header = new WebSocketHeader();
            header.Fin = (finOpcodeByte & 0b1000_0000) != 0;
            header.Opcode = (WebSocketOpcode)(finOpcodeByte & 0b0000_1111);

            if (extendedPayloadLengthSize == 2)
            {
                short length;

                reader.TryReadBigEndian(out length);
                header.PayloadLength = (ulong)length;
            }
            else if (extendedPayloadLengthSize == 8)
            {
                long length;

                reader.TryReadBigEndian(out length);
                header.PayloadLength = (ulong)length;
            }
            else
            {
                header.PayloadLength = payloadLength;
            }

            if (masked)
            {
                reader.TryReadBigEndian(out header.MaskingKey);
                header.Masked = true;
            }

            message = new WebSocketReadFrame
            {
                Header = header,
                Payload = new WebSocketPayloadReader(header)
            };
            return true;
        }
    }
}
