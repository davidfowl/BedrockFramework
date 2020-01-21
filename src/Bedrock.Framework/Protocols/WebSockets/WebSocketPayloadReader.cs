using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// Reads a WebSocket payload from a sequence.
    /// </summary>
    public struct WebSocketPayloadReader : IMessageReader<ReadOnlySequence<byte>>
    {
        /// <summary>
        /// The number of bytes remaining in the payload.
        /// </summary>
        private ulong _payloadBytesRemaining;

        /// <summary>
        /// Whether or not the payload is masked.
        /// </summary>
        private bool _masked;

        /// <summary>
        /// An instance of the encoder, for unmasking the payload if necessary.
        /// </summary>
        private WebSocketPayloadEncoder _payloadEncoder;

        /// <summary>
        /// Creates an instance of a WebSocketPayloadReader.
        /// </summary>
        /// <param name="header">The WebSocketHeader associated with this payload.</param>
        public WebSocketPayloadReader(WebSocketHeader header)
        {
            _payloadBytesRemaining = header.PayloadLength;
            _payloadEncoder = new WebSocketPayloadEncoder(header.MaskingKey);
            _masked = header.Masked;
        }

        /// <summary>
        /// Attempts to read the WebSocket payload from a sequence.
        /// </summary>
        /// <param name="input">The sequence to parse messages from.</param>
        /// <param name="consumed">The position in the sequence to which the parser has fully consumed.</param>
        /// <param name="examined">The position in the sequence to which the parser has examined but not yet consumed.</param>
        /// <param name="message">The payload data, unmasked if necessary. This will be default if the full payload has been read.</param>
        /// <returns>True if any data could be read or the payload is complete, false if the sequence was empty but payload data remains.</returns>
        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out ReadOnlySequence<byte> message)
        {
            message = input;

            if (_payloadBytesRemaining == 0)
            {
                message = default;
                consumed = input.Start;
                examined = input.Start;
                return true;
            }

            if (input.IsEmpty)
            {
                consumed = input.Start;
                examined = input.Start;
                return false;
            }

            long bytesRead = 0;
            if (!_masked)
            {
                bytesRead = Math.Min((long)_payloadBytesRemaining, input.Length);
                var position = input.GetPosition(bytesRead);

                consumed = position;
                examined = position;
            }
            else
            {
                bytesRead = _payloadEncoder.MaskUnmaskPayload(input, _payloadBytesRemaining, out consumed);
                examined = consumed;
            }

            _payloadBytesRemaining -= (ulong)bytesRead;
            return true;
        }
    }
}
