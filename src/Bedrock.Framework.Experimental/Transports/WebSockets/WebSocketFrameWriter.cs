using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    /// <summary>
    /// Writes a WebSocket frame message to a buffer.
    /// </summary>
    public struct WebSocketFrameWriter : IMessageWriter<WebSocketWriteFrame>
    {
        /// <summary>
        /// Writes the WebSocketWriteFrame to the provided buffer writer.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <param name="output">The buffer writer to write the message to.</param>
        public unsafe void WriteMessage(WebSocketWriteFrame message, IBufferWriter<byte> output)
        {
            if (message.Header.PayloadLength != (ulong)message.Payload.Length)
            {
                throw new WebSocketFrameException($"Header payload length ({message.Header.PayloadLength}) does not equal supplied payload length ({message.Payload.Length})");
            }

            var headerSize = 2;
            var extendedPayloadLengthSize = 0;
            var maskPosition = 2;

            if (message.Header.PayloadLength > 125)
            {
                if (message.Header.PayloadLength <= ushort.MaxValue)
                {
                    extendedPayloadLengthSize = 2;
                    maskPosition += 2;
                }
                else
                {
                    extendedPayloadLengthSize = 8;
                    maskPosition += 8;
                }
            }

            if (message.Header.Masked)
            {
                headerSize += extendedPayloadLengthSize + 4;
            }
            else
            {
                headerSize += extendedPayloadLengthSize;
            }

            Span<byte> headerSpan = (stackalloc byte[14]).Slice(0, headerSize);
            headerSpan[0] = (byte)message.Header.Opcode;

            if (message.Header.Fin)
            {
                headerSpan[0] |= 0b1000_0000;
            }

            switch (extendedPayloadLengthSize)
            {
                case 2:
                    headerSpan[1] = 126;
                    BinaryPrimitives.WriteUInt16BigEndian(headerSpan.Slice(2), (ushort)message.Header.PayloadLength);
                    break;
                case 8:
                    headerSpan[1] = 127;
                    BinaryPrimitives.WriteUInt64BigEndian(headerSpan.Slice(2), message.Header.PayloadLength);
                    break;
                default:
                    headerSpan[1] = (byte)message.Header.PayloadLength;
                    break;
            }

            if (message.Header.Masked)
            {
                headerSpan[1] |= 0b1000_0000;
                BinaryPrimitives.WriteInt32BigEndian(headerSpan, message.Header.MaskingKey);

                if (!message.MaskingComplete)
                {
                    var encoder = new WebSocketPayloadEncoder(message.Header.MaskingKey);
                    encoder.MaskUnmaskPayload(message.Payload, message.Header.PayloadLength, out var _);
                }
            }

            output.Write(headerSpan);
            if (message.Payload.IsSingleSegment)
            {
                output.Write(message.Payload.FirstSpan);
            }
            else
            {
                foreach (var memory in message.Payload)
                {
                    output.Write(memory.Span);
                }
            }
        }
    }
}
