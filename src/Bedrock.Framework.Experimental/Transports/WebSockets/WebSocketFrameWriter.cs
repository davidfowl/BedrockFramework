using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    public struct WebSocketFrameWriter : IMessageWriter<WebSocketWriteFrame>
    {
        private static readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();

        public unsafe void WriteMessage(WebSocketWriteFrame message, IBufferWriter<byte> output)
        {
            var writer = new BufferWriter<IBufferWriter<byte>>(output);

            var headerSize = 2;
            var extendedPayloadLengthSize = 0;
            var maskPosition = 2;

            if(message.Header.PayloadLength > 125)
            {
                if(message.Header.PayloadLength <= ushort.MaxValue)
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

            if(message.Header.Masked)
            {
                headerSize = headerSize + extendedPayloadLengthSize + 4;
            }
            else
            {
                headerSize = headerSize + extendedPayloadLengthSize;
            }

            Span<byte> headerSpan = stackalloc byte[headerSize];
            headerSpan[0] = (byte)message.Header.Opcode;

            if(message.Header.Fin)
            {
                headerSpan[0] |= 0b1000_0000;
            }

            switch(extendedPayloadLengthSize)
            {
                case 2:
                    headerSpan[1] = 126;
                    headerSpan[2] = (byte)(message.Header.PayloadLength >> 8);
                    headerSpan[3] = unchecked((byte)message.Header.PayloadLength);
                    break;
                case 8:
                    headerSpan[1] = 127;
                    ulong length = message.Header.PayloadLength;
                    for (int i = 9; i >= 2; i--)
                    {
                        headerSpan[i] = unchecked((byte)length);
                        length = length >> 8;
                    }
                    break;
                default:
                    headerSpan[2] = (byte)message.Header.PayloadLength;
                    break;
            }

            if(message.Header.Masked)
            {
                headerSpan[1] |= 0b1000_0000;
                headerSpan[maskPosition] = (byte)message.Header.MaskingKey;
                headerSpan[maskPosition + 1] = (byte)(message.Header.MaskingKey >> 8);
                headerSpan[maskPosition + 2] = (byte)(message.Header.MaskingKey >> 16);
                headerSpan[maskPosition + 3] = (byte)(message.Header.MaskingKey >> 24);

                output.Write(headerSpan);

            }
        }
    }
}