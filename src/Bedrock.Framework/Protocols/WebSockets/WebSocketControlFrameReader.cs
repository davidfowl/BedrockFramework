using Bedrock.Framework.Protocols;
using Bedrock.Framework.Protocols.WebSockets;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// Reads a WebSocket control frame payload.
    /// </summary>
    public readonly struct WebSocketControlFrameReader : IMessageReader<WebSocketControlFrame>
    {
        /// <summary>
        /// The header of the WebSocket control frame.
        /// </summary>
        private readonly WebSocketHeader _header;

        /// <summary>
        /// A payload reader instance for the control frame.
        /// </summary>
        private readonly WebSocketPayloadReader _payloadReader;

        /// <summary>
        /// Creates an instance of a WebSocketControlFrameReader.
        /// </summary>
        /// <param name="header">The header of the WebSocket control frame.</param>
        public WebSocketControlFrameReader(WebSocketHeader header)
        {
            _header = header;
            _payloadReader = new WebSocketPayloadReader(header);
        }

        /// <summary>
        /// Attempts to parse a WebSocket control frame payload from a sequence.
        /// </summary>
        /// <param name="input">The input sequence to parse from.</param>
        /// <param name="consumed">The position in the sequence that has been consumed.</param>
        /// <param name="examined">The position in the sequence that has been examined.</param>
        /// <param name="message">The returned WebSocket control frame message.</param>
        /// <returns>True if the message could be parsed, false otherwise.</returns>
        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out WebSocketControlFrame message)
        {
            if ((ulong)input.Length < _header.PayloadLength)
            {
                message = default;
                return false;
            }

            _payloadReader.TryParseMessage(input, ref consumed, ref examined, out var output);
            Debug.Assert(_payloadReader.BytesRemaining == 0);

            if (_header.Opcode == WebSocketOpcode.Close && output.Length >= 2)
            {
                var closeStatus = (WebSocketCloseStatus)BinaryPrimitives.ReadInt16BigEndian(output.FirstSpan);
                message = new WebSocketControlFrame(_header.Opcode, closeStatus, output.Slice(2));
            }
            else
            {
                message = new WebSocketControlFrame(_header.Opcode, default, output);
            }

            return true;
        }
    }
}
