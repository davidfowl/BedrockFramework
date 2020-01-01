using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Protocols
{
    public class WebSocketFrameReader : IMessageReader<MessageHeader>
    {
        private bool _isServer = false;

        private MessageHeader _lastReceiveHeader;

        /// <summary>The maximum size in bytes of a message frame header that includes mask bytes.</summary>
        internal const int MaxMessageHeaderLength = 14;
        /// <summary>The maximum size of a control message payload.</summary>
        private const int MaxControlPayloadLength = 125;
        /// <summary>Length of the mask XOR'd with the payload data.</summary>
        private const int MaskLength = 4;

        public WebSocketFrameReader()
        {

        }

        public bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out MessageHeader message)
        {
            var reader = new SequenceReader<byte>(input);

            MessageHeader header = _lastReceiveHeader;
            if (header.PayloadLength == 0)
            {
                if (input.Length < 2)
                {
                    consumed = input.Start;
                    examined = input.End;
                    message = default;
                    return false;
                }

                reader.TryRead(out var b1);
                reader.TryRead(out var b2);

                // Then make sure we have the full header based on the payload length.
                // If this is the server, we also need room for the received mask.
                long payloadLength = b2 & 0x7F;
                if (_isServer || payloadLength > 125)
                {
                    int minNeeded =
                        2 +
                        (_isServer ? MaskLength : 0) +
                        (payloadLength <= 125 ? 0 : payloadLength == 126 ? sizeof(ushort) : sizeof(ulong)); // additional 2 or 8 bytes for 16-bit or 64-bit length

                    if (reader.Remaining < minNeeded)
                    {
                        consumed = input.Start;
                        examined = input.End;
                        message = default;
                        return false;
                    }
                }

                if (!TryParseMessageHeaderFromReceiveBuffer(b1, b2, ref reader, out header))
                {
                    throw new InvalidDataException();
                }
            }

            //// If the header represents a ping or a pong, it's a control message meant
            //// to be transparent to the user, so handle it and then loop around to read again.
            //// Alternatively, if it's a close message, handle it and exit.
            //if (header.Opcode == MessageOpcode.Ping || header.Opcode == MessageOpcode.Pong)
            //{
            //    consumed = reader.Position;
            //    examined = consumed;
            //    message = default;
            //    return true;
            //}
            //else if (header.Opcode == MessageOpcode.Close)
            //{
            //    consumed = reader.Position;
            //    examined = consumed;
            //    return true;
            //}

            // If this is a continuation, replace the opcode with the one of the message it's continuing
            if (header.Opcode == MessageOpcode.Continuation)
            {
                header.Opcode = _lastReceiveHeader.Opcode;
            }

            if (_isServer)
            {
                // _receivedMaskOffsetOffset = ApplyMask(payloadBuffer.Span.Slice(0, totalBytesReceived), header.Mask, _receivedMaskOffsetOffset);
            }

            consumed = reader.Position;
            examined = consumed;
            message = header;

            // header.PayloadLength -= totalBytesReceived;

            // If this a text message, validate that it contains valid UTF8.
            //if (header.Opcode == MessageOpcode.Text &&
            //    !TryValidateUtf8(payloadBuffer.Span.Slice(0, totalBytesReceived), header.Fin && header.PayloadLength == 0, _utf8TextState))
            //{
            //    await CloseWithReceiveErrorAndThrowAsync(WebSocketCloseStatus.InvalidPayloadData, WebSocketError.Faulted).ConfigureAwait(false);
            //}

            // If there's no data to read, return an appropriate result.
            _lastReceiveHeader = header;
            return true;
        }

        private bool TryParseMessageHeaderFromReceiveBuffer(byte b1, byte b2, ref SequenceReader<byte> reader, out MessageHeader resultHeader)
        {
            Debug.Assert(reader.Remaining >= 2, $"Expected to at least have the first two bytes of the header.");

            MessageHeader header = default;

            header.Fin = (b1 & 0x80) != 0;
            bool reservedSet = (b1 & 0x70) != 0;
            header.Opcode = (MessageOpcode)(b1 & 0xF);

            bool masked = (b2 & 0x80) != 0;
            header.PayloadLength = b2 & 0x7F;

            // ConsumeFromBuffer(2);

            // Read the remainder of the payload length, if necessary
            if (header.PayloadLength == 126)
            {
                reader.TryRead(out b1);
                reader.TryRead(out b2);
                // Debug.Assert(_receiveBufferCount >= 2, $"Expected to have two bytes for the payload length.");
                header.PayloadLength = (b1 << 8) | b2;
            }
            else if (header.PayloadLength == 127)
            {
                // Debug.Assert(_receiveBufferCount >= 8, $"Expected to have eight bytes for the payload length.");
                header.PayloadLength = 0;
                for (int i = 0; i < 8; i++)
                {
                    reader.TryRead(out b1);
                    header.PayloadLength = (header.PayloadLength << 8) | b1;
                }

                // ConsumeFromBuffer(8);
            }

            bool shouldFail = reservedSet;
            if (masked)
            {
                if (!_isServer)
                {
                    shouldFail = true;
                }
                //  private static unsafe int CombineMaskBytes(Span<byte> buffer, int maskOffset) =>
                //BitConverter.ToInt32(buffer.Slice(maskOffset));
                reader.TryReadLittleEndian(out int mask);
                header.Mask = mask; // CombineMaskBytes(receiveBufferSpan, _receiveBufferOffset);

                // Consume the mask bytes
                // ConsumeFromBuffer(4);
            }

            // Do basic validation of the header
            switch (header.Opcode)
            {
                case MessageOpcode.Continuation:
                    if (_lastReceiveHeader.Fin)
                    {
                        // Can't continue from a final message
                        shouldFail = true;
                    }
                    break;

                case MessageOpcode.Binary:
                case MessageOpcode.Text:
                    if (!_lastReceiveHeader.Fin)
                    {
                        // Must continue from a non-final message
                        shouldFail = true;
                    }
                    break;

                case MessageOpcode.Close:
                case MessageOpcode.Ping:
                case MessageOpcode.Pong:
                    if (header.PayloadLength > MaxControlPayloadLength || !header.Fin)
                    {
                        // Invalid control messgae
                        shouldFail = true;
                    }
                    break;

                default:
                    // Unknown opcode
                    shouldFail = true;
                    break;
            }

            // Return the read header
            resultHeader = header;
            return !shouldFail;
        }
    }

    public enum MessageOpcode : byte
    {
        Continuation = 0x0,
        Text = 0x1,
        Binary = 0x2,
        Close = 0x8,
        Ping = 0x9,
        Pong = 0xA
    }

    [StructLayout(LayoutKind.Auto)]
    public struct MessageHeader
    {
        public MessageOpcode Opcode;
        public bool Fin;
        public long PayloadLength;
        public int Mask;
    }
}
