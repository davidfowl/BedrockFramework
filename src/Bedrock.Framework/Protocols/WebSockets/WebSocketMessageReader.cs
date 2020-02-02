using Bedrock.Framework.Protocols;
using Bedrock.Framework.Protocols.WebSockets;
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
    /// A reader-like construct for reading the content of WebSocket messages.
    /// </summary>
    public class WebSocketMessageReader
    {
        /// <summary>
        /// The maximum allowable control frame payload length, per RFC 6455
        /// </summary>
        private const int MaxControlFramePayloadLength = 125;

        /// <summary>
        /// Whether or not the reader is in a state where it requires reading a header. True
        /// if a header needs to be read, false if the frame payload is still being read.
        /// </summary>
        private bool _awaitingHeader;

        /// <summary>
        /// Whether or not the underlying transport is completed.
        /// </summary>
        private bool _isCompleted;

        /// <summary>
        /// Whether or not the underlying transport is canceled.
        /// </summary>
        private bool _isCanceled;

        /// <summary>
        /// Whether or not the current message is a Text type message and should be validated
        /// for UTF-8 correctness.
        /// </summary>
        private bool _isText;

        /// <summary>
        /// The header of the currently read frame.
        /// </summary>
        private WebSocketHeader _header;

        /// <summary>
        /// The payload reader for the currently read frame.
        /// </summary>
        private WebSocketPayloadReader _payloadReader;

        /// <summary>
        /// The protocol reader for consuming data from the transport.
        /// </summary>
        private ProtocolReader _protocolReader;

        /// <summary>
        /// An instance of the WebSocket frame reader.
        /// </summary>
        private WebSocketFrameReader _frameReader = new WebSocketFrameReader();

        /// <summary>
        /// An instance of the control frame handler for handling control frame flow.
        /// </summary>
        private IControlFrameHandler _controlFrameHandler;

        /// <summary>
        /// The options for this reader.
        /// </summary>
        private PipeOptions _options;

        /// <summary>
        /// A buffer for buffering unconsumed read data.
        /// </summary>
        private ConsumableArrayBufferWriter _buffer = new ConsumableArrayBufferWriter();

        /// <summary>
        /// The current sequence being processed.
        /// </summary>
        private ReadOnlySequence<byte> _currentSequence;

        /// <summary>
        /// Creates an instance of a WebSocketMessageReader.
        /// </summary>
        /// <param name="transport">The underlying transport to read from.</param>
        /// <param name="controlFrameHandler">A control frame handler to use with this instance.</param>
        /// <param name="options">Options for this reader.</param>
        public WebSocketMessageReader(PipeReader transport, IControlFrameHandler controlFrameHandler, PipeOptions options = default)
        {
            _controlFrameHandler = controlFrameHandler;
            _awaitingHeader = true;
            _protocolReader = new ProtocolReader(transport);
            _options = options ?? PipeOptions.Default;
        }

        /// <summary>
        /// Reads a portion of the message from the reader.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        /// <returns>A message read result.</returns>
        public ValueTask<MessageReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (_awaitingHeader)
            {
                var readTask = GetNextMessageFrameAsync(cancellationToken);
                if(readTask.IsCompletedSuccessfully)
                {
                    var frame = readTask.Result;
                    ValidateHeader(frame.Header);

                    _header = frame.Header;
                    _payloadReader = frame.Payload;
                }
                else
                {
                    return DoReadHeaderRequiredAsync(readTask, cancellationToken);
                }
            }

            return ReadPayloadAsync(cancellationToken);
        }

        /// <summary>
        /// Completes an async read when reading a header is required.
        /// </summary>
        /// <param name="readTask">The active async read task from the ProtocolReader.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A MessageReadResult.</returns>
        private async ValueTask<MessageReadResult> DoReadHeaderRequiredAsync(ValueTask<WebSocketReadFrame> readTask, CancellationToken cancellationToken)
        {
            var frame = await readTask.ConfigureAwait(false);

            ValidateHeader(frame.Header);

            _header = frame.Header;
            _payloadReader = frame.Payload;

            return await ReadPayloadAsync(cancellationToken);
        }

        /// <summary>
        /// Reads a portion of a message payload.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A MessageReadResult.</returns>
        private ValueTask<MessageReadResult> ReadPayloadAsync(CancellationToken cancellationToken)
        {
            //Don't keep reading data into the buffer if we've hit a threshold
            //TODO: Is this even the right value to use in this context?
            if (_buffer.UnconsumedWrittenCount < _options.PauseWriterThreshold)
            {
                var readTask = _protocolReader.ReadAsync(_payloadReader, cancellationToken);
                if (readTask.IsCompletedSuccessfully)
                {
                    PopulateFromRead(readTask.Result);
                }
                else
                {
                    return CreateMessageReadResultAsync(readTask, cancellationToken);
                }   
            }

            var endOfMessage = _header.Fin && _payloadReader.BytesRemaining == 0;

            //Serve back buffered data, if it exists, else give the direct sequence without buffering
            if (_buffer.UnconsumedWrittenCount > 0)
            {
                return new ValueTask<MessageReadResult>(
                    new MessageReadResult(new ReadOnlySequence<byte>(_buffer.WrittenMemory), endOfMessage, _isCanceled, _isCompleted));
            }
            else
            {
                return new ValueTask<MessageReadResult>(new MessageReadResult(_currentSequence, endOfMessage, _isCanceled, _isCompleted));
            }
        }

        /// <summary>
        /// Creates a new MessageReadResult asynchronously.
        /// </summary>
        /// <param name="readTask">The active read task from the ProtocolReader.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A new MessageReadResult.</returns>
        private async ValueTask<MessageReadResult> CreateMessageReadResultAsync(ValueTask<ProtocolReadResult<ReadOnlySequence<byte>>> readTask, CancellationToken cancellationToken)
        {
            PopulateFromRead(await readTask);

            var endOfMessage = _header.Fin && _payloadReader.BytesRemaining == 0;

            //Serve back buffered data, if it exists, else give the direct sequence without buffering
            if (_buffer.UnconsumedWrittenCount > 0)
            {
                return new MessageReadResult(new ReadOnlySequence<byte>(_buffer.WrittenMemory), endOfMessage, _isCanceled, _isCompleted);
            }
            else
            {
                return new MessageReadResult(_currentSequence, endOfMessage, _isCanceled, _isCompleted);
            }
        }

        /// <summary>
        /// Populates the message reader from a payload read result.
        /// </summary>
        /// <param name="readResult">The read result to populate the message reader from.</param>
        private void PopulateFromRead(ProtocolReadResult<ReadOnlySequence<byte>> readResult)
        {
            if (readResult.IsCanceled)
            {
                throw new OperationCanceledException("Read canceled while attempting to read WebSocket payload.");
            }

            var sequence = readResult.Message;

            //If there is already data in the buffer, we'll need to add to it
            if (_buffer.UnconsumedWrittenCount > 0)
            {
                if (sequence.IsSingleSegment)
                {
                    _buffer.Write(sequence.FirstSpan);
                }
                else
                {
                    foreach (var segment in sequence)
                    {
                        _buffer.Write(segment.Span);
                    }
                }
            }

            _currentSequence = readResult.Message;
            _isCompleted = readResult.IsCompleted;
            _isCanceled = readResult.IsCanceled;

            _awaitingHeader = _payloadReader.BytesRemaining == 0;
        }

        /// <summary>
        /// Advances the reader to the provided position.
        /// </summary>
        /// <param name="consumed">The position that has been consumed.</param>
        public void AdvanceTo(SequencePosition consumed)
        {
            AdvanceTo(consumed, consumed);
        }

        /// <summary>
        /// Advances the reader to the provided position.
        /// </summary>
        /// <param name="consumed">The position that has been consumed.</param>
        /// <param name="examined">The position that has been examined.</param>
        public void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            _protocolReader.Advance();

            //If we buffered the message, advance the buffer
            if (_buffer.UnconsumedWrittenCount > 0)
            {
                var lengthConsumed = _currentSequence.Slice(0, consumed).Length;
                _buffer.Consume((int)lengthConsumed);
            }

            //If we didn't consume the entire current sequence and we didn't previously buffer it, 
            //we'll have to buffer it for future reads           
            if (_buffer.UnconsumedWrittenCount == 0)
            {
                var unconsumedSequence = _currentSequence.Slice(consumed);
                if (unconsumedSequence.Length != 0)
                {
                    if (unconsumedSequence.IsSingleSegment)
                    {
                        _buffer.Write(unconsumedSequence.FirstSpan);
                    }
                    else
                    {
                        foreach (var segment in unconsumedSequence)
                        {
                            _buffer.Write(segment.Span);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Moves the reader to the next message.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        /// <returns>True if the message is text, false otherwise.</returns>
        public ValueTask<bool> MoveNextMessageAsync(CancellationToken cancellationToken = default)
        {
            if (_payloadReader is object && _payloadReader.BytesRemaining != 0)
            {
                throw new InvalidOperationException("MoveNextMessageAsync cannot be called while a message is still being read.");
            }

            var readTask = GetNextMessageFrameAsync(cancellationToken);
            if (readTask.IsCompletedSuccessfully)
            {
                return new ValueTask<bool>(SetNextMessageAndGetIsText(readTask.Result));
            }

            return DoSetNextMessageAsync(readTask);
        }

        /// <summary>
        /// Sets the next message frame asynchronously.
        /// </summary>
        /// <param name="readTask">The active ProtocolReader read task.</param>
        /// <returns>True if the next message is a text message, false otherwise.</returns>
        private async ValueTask<bool> DoSetNextMessageAsync(ValueTask<WebSocketReadFrame> readTask)
        {
            return SetNextMessageAndGetIsText(await readTask);
        }

        /// <summary>
        /// Sets the message reader up with the next message frame data and determines if the message
        /// is a text or binary message.
        /// </summary>
        /// <param name="frame">The read frame to set the message reader with.</param>
        /// <returns>True if the next message is text, false otherwise.</returns>
        private bool SetNextMessageAndGetIsText(WebSocketReadFrame frame)
        {
            if (frame.Header.Opcode != WebSocketOpcode.Binary && frame.Header.Opcode != WebSocketOpcode.Text)
            {
                ThrowBadProtocol($"Expected a start of message frame of Binary or Text but received {frame.Header.Opcode} instead.");
            }

            _header = frame.Header;
            _payloadReader = frame.Payload;

            _isText = _header.Opcode == WebSocketOpcode.Text;
            _awaitingHeader = false;

            return _isText;
        }

        /// <summary>
        /// Gets the next message frame from the transport.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        /// <returns>A new WebSocket read frame.</returns>
        private ValueTask<WebSocketReadFrame> GetNextMessageFrameAsync(CancellationToken cancellationToken)
        {
            var readTask = _protocolReader.ReadAsync(_frameReader, cancellationToken);
            ProtocolReadResult<WebSocketReadFrame> frame;

            if (readTask.IsCompletedSuccessfully)
            {
                frame = readTask.Result;
            }
            else
            {
                return DoGetNextMessageAsync(readTask, cancellationToken);
            }

            if (frame.IsCanceled)
            {
                throw new OperationCanceledException("Read canceled while attempting to read WebSocket frame.");
            }

            var header = frame.Message.Header;
            if (!(header.Opcode == WebSocketOpcode.Ping || header.Opcode == WebSocketOpcode.Pong || header.Opcode == WebSocketOpcode.Close))
            {
                _protocolReader.Advance();
                return new ValueTask<WebSocketReadFrame>(frame.Message);
            }

            return DoGetNextMessageAsync(readTask, cancellationToken);
        }

        /// <summary>
        /// Gets the next message frame from the transport as an async call.
        /// </summary>
        /// <param name="readTask">The active protocol reader ReadAsync task to await.</param>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        /// <returns>A new WebSocket read frame.</returns>
        private async ValueTask<WebSocketReadFrame> DoGetNextMessageAsync(ValueTask<ProtocolReadResult<WebSocketReadFrame>> readTask, CancellationToken cancellationToken)
        {
            while (true)
            {
                var frame = await readTask.ConfigureAwait(false);
                _protocolReader.Advance();

                if (frame.IsCanceled)
                {
                    throw new OperationCanceledException("Read canceled while attempting to read WebSocket frame.");
                }

                var header = frame.Message.Header;
                if (header.Opcode == WebSocketOpcode.Ping || header.Opcode == WebSocketOpcode.Pong || header.Opcode == WebSocketOpcode.Close)
                {
                    var controlFrame = await _protocolReader.ReadAsync(new WebSocketControlFrameReader(header), cancellationToken).ConfigureAwait(false);
                    _protocolReader.Advance();

                    if (controlFrame.IsCanceled)
                    {
                        throw new OperationCanceledException("Read canceled while attempting to read WebSocket frame.");
                    }

                    await _controlFrameHandler.HandleControlFrameAsync(controlFrame.Message);
                }
                else
                {
                    return frame.Message;
                }

                readTask = _protocolReader.ReadAsync(new WebSocketFrameReader(), cancellationToken);
            }
        }

        /// <summary>
        /// Validates that a header follows the proper protocol specification.
        /// </summary>
        /// <param name="newHeader">The new header being validated against.</param>
        private void ValidateHeader(WebSocketHeader newHeader)
        {
            switch (newHeader.Opcode)
            {
                case WebSocketOpcode.Continuation:
                    if (_header.Fin)
                    {
                        ThrowBadProtocol("Unexpected continuation frame received after final message frame.");
                    }
                    break;

                case WebSocketOpcode.Binary:
                case WebSocketOpcode.Text:
                    ThrowBadProtocol($"Expected continuation frame after non-final frame but received opcode {newHeader.Opcode} instead.");
                    break;

                case WebSocketOpcode.Close:
                case WebSocketOpcode.Ping:
                case WebSocketOpcode.Pong:
                    if (newHeader.PayloadLength > MaxControlFramePayloadLength || !newHeader.Fin)
                    {
                        ThrowBadProtocol("Control frame contained invalid data.");
                    }
                    break;

                default:
                    ThrowBadProtocol("Received unknown opcode.");
                    break;
            }
        }

        /// <summary>
        /// Throws a protocol exception.
        /// </summary>
        /// <param name="message">The message to include.</param>
        private void ThrowBadProtocol(string message)
        {
            throw new WebSocketProtocolException(message);
        }
    }
}
