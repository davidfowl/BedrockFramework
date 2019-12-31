using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class ProtocolReader : IAsyncDisposable
    {
        private SequencePosition _examined;
        private SequencePosition _consumed;
        private ReadOnlySequence<byte> _buffer;
        private bool _isCanceled;
        private bool _isCompleted;
        private bool _hasMessage;
        private bool _disposed;

        public ProtocolReader(ConnectionContext connection)
        {
            Connection = connection;
        }

        public ConnectionContext Connection { get; }

        public ValueTask<ProtocolReadResult<TReadMessage>> ReadAsync<TReadMessage>(IMessageReader<TReadMessage> reader, CancellationToken cancellationToken = default)
        {
            return ReadAsync(reader, maximumMessageSize: null, cancellationToken);
        }

        public ValueTask<ProtocolReadResult<TReadMessage>> ReadAsync<TReadMessage>(IMessageReader<TReadMessage> reader, int maximumMessageSize, CancellationToken cancellationToken = default)
        {
            return ReadAsync(reader, (int?)maximumMessageSize, cancellationToken);
        }

        public ValueTask<ProtocolReadResult<TReadMessage>> ReadAsync<TReadMessage>(IMessageReader<TReadMessage> reader, int? maximumMessageSize, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (_hasMessage)
            {
                throw new InvalidOperationException($"{nameof(Advance)} must be called before calling {nameof(ReadAsync)}");
            }

            // If this is the very first read, then make it go async since we have no data
            if (_consumed.GetObject() == null)
            {
                return DoAsyncRead(maximumMessageSize, reader, cancellationToken);
            }

            // We have a buffer, test to see if there's any message left in the buffer
            if (TryParseMessage(maximumMessageSize, reader, _buffer, out var protocolMessage))
            {
                _hasMessage = true;
                return new ValueTask<ProtocolReadResult<TReadMessage>>(new ProtocolReadResult<TReadMessage>(protocolMessage, _isCanceled, isCompleted: false));
            }
            else
            {
                // We couldn't parse the message so advance the input so we can read
                Connection.Transport.Input.AdvanceTo(_consumed, _examined);
            }

            if (_isCompleted)
            {
                _consumed = default;
                _examined = default;

                // If we're complete then short-circuit
                if (!_buffer.IsEmpty)
                {
                    throw new InvalidDataException("Connection terminated while reading a message.");
                }

                return new ValueTask<ProtocolReadResult<TReadMessage>>(new ProtocolReadResult<TReadMessage>(default, _isCanceled, _isCompleted));
            }

            return DoAsyncRead(maximumMessageSize, reader, cancellationToken);
        }

        private async ValueTask<ProtocolReadResult<TReadMessage>> DoAsyncRead<TReadMessage>(int? maximumMessageSize, IMessageReader<TReadMessage> reader, CancellationToken cancellationToken)
        {
            var input = Connection.Transport.Input;

            while (true)
            {
                var result = await input.ReadAsync(cancellationToken).ConfigureAwait(false);

                _buffer = result.Buffer;
                _isCanceled = result.IsCanceled;
                _isCompleted = result.IsCompleted;
                _consumed = _buffer.Start;
                _examined = _buffer.End;

                if (_isCanceled)
                {
                    break;
                }

                if (TryParseMessage(maximumMessageSize, reader, _buffer, out var protocolMessage))
                {
                    _hasMessage = true;
                    return new ProtocolReadResult<TReadMessage>(protocolMessage, _isCanceled, isCompleted: false);
                }
                else
                {
                    input.AdvanceTo(_consumed, _examined);
                }

                if (_isCompleted)
                {
                    _consumed = default;
                    _examined = default;

                    if (!_buffer.IsEmpty)
                    {
                        throw new InvalidDataException("Connection terminated while reading a message.");
                    }

                    break;
                }
            }

            return new ProtocolReadResult<TReadMessage>(default, _isCanceled, _isCompleted);
        }

        private bool TryParseMessage<TReadMessage>(int? maximumMessageSize, IMessageReader<TReadMessage> reader, in ReadOnlySequence<byte> buffer, out TReadMessage protocolMessage)
        {
            // No message limit, just parse and dispatch
            if (maximumMessageSize == null)
            {
                if (reader.TryParseMessage(buffer, out _consumed, out _examined, out protocolMessage))
                {
                    return true;
                }

                return false;
            }

            // We give the parser a sliding window of the default message size
            var maxMessageSize = maximumMessageSize.Value;

            var segment = buffer;
            var overLength = false;

            if (segment.Length > maxMessageSize)
            {
                segment = segment.Slice(segment.Start, maxMessageSize);
                overLength = true;
            }

            if (reader.TryParseMessage(segment, out _consumed, out _examined, out protocolMessage))
            {
                return true;
            }
            else if (overLength)
            {
                throw new InvalidDataException($"The maximum message size of {maxMessageSize}B was exceeded.");
            }

            return false;
        }

        public void Advance()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            _isCanceled = false;

            if (!_hasMessage)
            {
                return;
            }

            _buffer = _buffer.Slice(_consumed);

            _hasMessage = false;
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return default;
        }
    }
}
