using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Protocols.WebSockets
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
            message = default;

            //We need to at least be able to read the start of frame header
            if (input.Length < 2)
            {
                return false;
            }

            var reader = new SequenceReader<byte>(input);
            reader.TryRead(out var finOpcodeByte);
            reader.TryRead(out var maskLengthByte);

            var fin = (finOpcodeByte & 0b1000_0000) != 0;
            var rsv1 = (finOpcodeByte & 0b0100_0000) != 0;
            var rsv2 = (finOpcodeByte & 0b0010_0000) != 0;
            var rsv3 = (finOpcodeByte & 0b0001_0000) != 0;
            var opcode = (WebSocketOpcode)(finOpcodeByte & 0b0000_1111);

            var masked = (maskLengthByte & 0b1000_0000) != 0;
            var initialPayloadLength = (ulong)(maskLengthByte & 0b0111_1111);

            // RFC 6455 5.2: reserved opcodes must fail the connection
            if (!IsKnownOpcode(opcode))
            {
                throw new WebSocketFrameException($"Received a frame with reserved opcode 0x{(byte)opcode:X}.");
            }

            // RFC 6455 5.5: control frames must not be fragmented and must have a payload of 125 bytes or less
            if (((byte)opcode & 0b0000_1000) != 0)
            {
                if (!fin)
                {
                    throw new WebSocketFrameException($"Received a fragmented {opcode} control frame.");
                }

                if (initialPayloadLength > 125)
                {
                    throw new WebSocketFrameException($"Received a {opcode} control frame with a payload larger than 125 bytes.");
                }
            }

            var maskSize = masked ? 4 : 0;
            var extendedPayloadLengthSize = initialPayloadLength switch
            {
                126 => 2,
                127 => 8,
                _ => 0
            };

            if (reader.Remaining < extendedPayloadLengthSize + maskSize)
            {
                return false;
            }

            ulong payloadLength;
            if (extendedPayloadLengthSize == 2)
            {
                reader.TryReadBigEndian(out short length);
                payloadLength = (ushort)length;
            }
            else if (extendedPayloadLengthSize == 8)
            {
                reader.TryReadBigEndian(out long length);

                // RFC 6455 5.2: the most significant bit of a 64-bit length must be 0
                if (length < 0)
                {
                    throw new WebSocketFrameException("Received a frame with a 64-bit payload length that has the most significant bit set.");
                }

                payloadLength = (ulong)length;
            }
            else
            {
                payloadLength = initialPayloadLength;
            }

            int maskingKey = 0;
            if (masked)
            {
                Span<byte> maskBytes = stackalloc byte[sizeof(int)];
                reader.TryCopyTo(maskBytes);

                maskingKey = BitConverter.ToInt32(maskBytes);
                reader.Advance(sizeof(int));
            }

            var header = new WebSocketHeader(fin, opcode, masked, payloadLength, maskingKey, rsv1, rsv2, rsv3);
            message = new WebSocketReadFrame(header, new WebSocketPayloadReader(header));

            consumed = reader.Position;
            examined = consumed;
            return true;
        }

        private static bool IsKnownOpcode(WebSocketOpcode opcode) => opcode switch
        {
            WebSocketOpcode.Continuation or WebSocketOpcode.Text or WebSocketOpcode.Binary or
            WebSocketOpcode.Close or WebSocketOpcode.Ping or WebSocketOpcode.Pong => true,
            _ => false
        };
    }
}
