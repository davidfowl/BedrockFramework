using Microsoft.AspNetCore.Connections;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// A WebSocket server and client protocol.
    /// </summary>
    /// <remarks>
    /// A single WebSocketProtocol instance should not be used from multiple threads simultaneously.
    /// </remarks>
    public class WebSocketProtocol : IControlFrameHandler
    {
        /// <summary>
        /// Whether or not the WebSocket has completed a close handshake.
        /// </summary>
        public bool IsClosed { get; private set; }

        /// <summary>
        /// The underlying transport.
        /// </summary>
        private IDuplexPipe _transport;

        /// <summary>
        /// The shared WebSocket message reader.
        /// </summary>
        private WebSocketMessageReader _messageReader;

        /// <summary>
        /// The type of WebSocket protocol, server or client.
        /// </summary>
        private WebSocketProtocolType _protocolType;

        /// <summary>
        /// Creates an instance of a WebSocketProtocol.
        /// </summary>
        /// <param name="connection">The connection context to use.</param>
        /// <param name="protocolType">The type of WebSocket protocol, server or client.</param>
        public WebSocketProtocol(ConnectionContext connection, WebSocketProtocolType protocolType)
        {
            _transport = connection.Transport;
            _messageReader = new WebSocketMessageReader(_transport.Input, this);
            _protocolType = protocolType;
        }

        /// <summary>
        /// Reads the next message from the WebSocket.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        /// <returns>A WebSocketReadResult.</returns>
        public ValueTask<WebSocketReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            var readTask = _messageReader.MoveNextMessageAsync(cancellationToken);

            if (readTask.IsCompletedSuccessfully)
            {
                return new ValueTask<WebSocketReadResult>(new WebSocketReadResult(readTask.Result, _messageReader));
            }
            else
            {
                return DoReadAsync(readTask, cancellationToken);
            }
        }

        /// <summary>
        /// Reads the next message from the WebSocket asynchronously.
        /// </summary>
        /// <param name="readTask">The active message reader task.</param>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        /// <returns>A WebSocketReadResult.</returns>
        private async ValueTask<WebSocketReadResult> DoReadAsync(ValueTask<bool> readTask, CancellationToken cancellationToken)
        {
            return new WebSocketReadResult(await readTask.ConfigureAwait(false), _messageReader);
        }

        /// <summary>
        /// Writes a message to the WebSocket.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <param name="isText">True if the message is a text type message, false otherwise.</param>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        public async ValueTask WriteMessageAsync(ReadOnlySequence<byte> message, bool isText, CancellationToken cancellationToken = default)
        {
            if (IsClosed)
            {
                throw new InvalidOperationException("A close message was already received from the remote endpoint.");
            }

            var opcode = isText ? WebSocketOpcode.Text : WebSocketOpcode.Binary;
            var masked = _protocolType == WebSocketProtocolType.Client;

            var header = new WebSocketHeader(true, opcode, masked, (ulong)message.Length, WebSocketHeader.GenerateMaskingKey());

            var frame = new WebSocketWriteFrame(header, message);
            var writer = new WebSocketFrameWriter();

            writer.WriteMessage(frame, _transport.Output);
            await _transport.Output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles a WebSocket control frame.
        /// </summary>
        /// <param name="controlFrame">The control frame to handle.</param>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        async ValueTask IControlFrameHandler.HandleControlFrameAsync(WebSocketControlFrame controlFrame, CancellationToken cancellationToken)
        {
            var masked = _protocolType == WebSocketProtocolType.Client;
            var maskingKey = _protocolType == WebSocketProtocolType.Client ? WebSocketHeader.GenerateMaskingKey() : default;

            if (controlFrame.Opcode == WebSocketOpcode.Ping)
            {
                var header = new WebSocketHeader(true, WebSocketOpcode.Pong, masked, (ulong)controlFrame.Payload.Length, maskingKey);
                var frame = new WebSocketWriteFrame(header, controlFrame.Payload);

                var writer = new WebSocketFrameWriter();

                writer.WriteMessage(frame, _transport.Output);
                await _transport.Output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (controlFrame.Opcode == WebSocketOpcode.Close)
            {
                var header = new WebSocketHeader(true, WebSocketOpcode.Pong, masked, (ulong)controlFrame.Payload.Length, maskingKey);
                var frame = new WebSocketWriteFrame(header, controlFrame.Payload);

                var writer = new WebSocketFrameWriter();

                writer.WriteMessage(frame, _transport.Output);
                await _transport.Output.FlushAsync(cancellationToken).ConfigureAwait(false);

                if (_protocolType == WebSocketProtocolType.Server)
                {
                    await _transport.Output.CompleteAsync().ConfigureAwait(false);
                }

                IsClosed = true;
            }
        }
    }
}
