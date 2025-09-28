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
    /// A writer-like construct for writing WebSocket messages.
    /// </summary>
    public class WebSocketMessageWriter
    {
        /// <summary>
        /// True if a message is in progress, false otherwise.
        /// </summary>
        internal bool _messageInProgress;

        /// <summary>
        /// The current protocol type for this writer, client or server.
        /// </summary>
        private WebSocketProtocolType _protocolType;

        /// <summary>
        /// The transport to write to.
        /// </summary>
        private PipeWriter _transport;

        /// <summary>
        /// True if the current message is text, false otherwise.
        /// </summary>
        public bool _isText;

        /// <summary>
        /// Creates an instance of a WebSocketMessageWriter.
        /// </summary>
        /// <param name="transport">The transport to write to.</param>
        /// <param name="protocolType">The protocol type for this writer.</param>
        public WebSocketMessageWriter(PipeWriter transport, WebSocketProtocolType protocolType)
        {
            _transport = transport;
            _protocolType = protocolType;
        }

        /// <summary>
        /// Starts a message in the writer.
        /// </summary>
        /// <param name="payload">The payload to write.</param>
        /// <param name="isText">Whether the payload is text or not.</param>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        internal ValueTask StartMessageAsync(ReadOnlySequence<byte> payload, bool isText, CancellationToken cancellationToken = default)
        {
            if(_messageInProgress)
            {
                ThrowMessageAlreadyStarted();
            }

            _messageInProgress = true;
            _isText = isText;
            return DoWriteAsync(isText ? WebSocketOpcode.Text : WebSocketOpcode.Binary, false, payload, cancellationToken);
        }

        /// <summary>
        /// Writes a single frame message with the writer.
        /// </summary>
        /// <param name="payload">The payload to write.</param>
        /// <param name="isText">Whether the payload is text or not.</param>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        internal ValueTask WriteSingleFrameMessageAsync(ReadOnlySequence<byte> payload, bool isText, CancellationToken cancellationToken = default)
        {
            if (_messageInProgress)
            {
                ThrowMessageAlreadyStarted();
            }

            var result = DoWriteAsync(isText ? WebSocketOpcode.Text : WebSocketOpcode.Binary, true, payload, cancellationToken);
            _messageInProgress = false;

            return result;
        }

        /// <summary>
        /// Writes a message payload portion with the writer.
        /// </summary>
        /// <param name="payload">The payload to write.</param>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        public ValueTask WriteAsync(ReadOnlySequence<byte> payload, CancellationToken cancellationToken = default)
        {
            if (!_messageInProgress)
            {
                ThrowMessageNotStarted();
            }

            return DoWriteAsync(WebSocketOpcode.Continuation, false, payload, cancellationToken);
        }

        /// <summary>
        /// Ends a message in progress.
        /// </summary>
        /// <param name="payload">The payload to write.</param>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        public ValueTask EndMessageAsync(ReadOnlySequence<byte> payload, CancellationToken cancellationToken = default)
        {
            if(!_messageInProgress)
            {
                ThrowMessageNotStarted();
            }

            var result = DoWriteAsync(WebSocketOpcode.Continuation, true, payload, cancellationToken);
            _messageInProgress = false;

            return result;
        }

        /// <summary>
        /// Sends a message payload portion.
        /// </summary>
        /// <param name="opcode">The WebSocket opcode to send.</param>
        /// <param name="endOfMessage">Whether or not this payload portion represents the end of the message.</param>
        /// <param name="payload">The payload to send.</param>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        private ValueTask DoWriteAsync(WebSocketOpcode opcode, bool endOfMessage, ReadOnlySequence<byte> payload, CancellationToken cancellationToken)
        {
            var masked = _protocolType == WebSocketProtocolType.Client;
            var header = new WebSocketHeader(endOfMessage, opcode, masked, (ulong)payload.Length, masked ? WebSocketHeader.GenerateMaskingKey() : 0);

            var frame = new WebSocketWriteFrame(header, payload);
            var writer = new WebSocketFrameWriter();

            writer.WriteMessage(frame, _transport);
            var flushTask = _transport.FlushAsync(cancellationToken);
            if (flushTask.IsCompletedSuccessfully)
            {
                var result = flushTask.Result;
                if(result.IsCanceled)
                {
                    ThrowMessageCanceled();
                }

                if (result.IsCompleted && !endOfMessage)
                {
                    ThrowTransportClosed();
                }

                return new ValueTask();
            }
            else
            {
                return PerformFlushAsync(flushTask, endOfMessage);
            }
        }

        /// <summary>
        /// Performs a flush of the writer asynchronously.
        /// </summary>
        /// <param name="flushTask">The active writer flush task.</param>
        /// <param name="endOfMessage">Whether or not this flush will send an end-of-message.</param>
        /// <returns></returns>
        private async ValueTask PerformFlushAsync(ValueTask<FlushResult> flushTask, bool endOfMessage)
        {
            var result = await flushTask.ConfigureAwait(false);
            if (result.IsCanceled)
            {
                ThrowMessageCanceled();
            }

            if (result.IsCompleted && !endOfMessage)
            {
                ThrowTransportClosed();
            }
        }
        
        /// <summary>
        /// Throws that a message was canceled unexpectedly.
        /// </summary>
        private void ThrowMessageCanceled()
        {
            throw new OperationCanceledException("Flush was canceled while a write was still in progress.");
        }

        /// <summary>
        /// Throws that the underlying transport closed unexpectedly.
        /// </summary>
        private void ThrowTransportClosed()
        {
            throw new InvalidOperationException("Transport closed unexpectedly while a message is still in progress.");
        }

        /// <summary>
        /// Throws if a message has not yet been started.
        /// </summary>
        private void ThrowMessageNotStarted()
        {
            throw new InvalidOperationException("Cannot end a message if a message has not been started.");
        }

        /// <summary>
        /// Throws if a message has already been started.
        /// </summary>
        private void ThrowMessageAlreadyStarted()
        {
            throw new InvalidOperationException("Cannot start a message when a message is already in progress.");
        }
    }
}
