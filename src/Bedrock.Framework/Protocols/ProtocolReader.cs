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
        private readonly int? _maximumMessageSize;
        private SequencePosition _examined;
        private SequencePosition _consumed;
        private ReadOnlySequence<byte> _buffer;
        private bool _isCanceled;
        private bool _isCompleted;
        private bool _hasMessage;
        private bool _disposed;

        public ProtocolReader(ConnectionContext connection, int? maximumMessageSize)
        {
            Connection = connection;
            _maximumMessageSize = maximumMessageSize;
        }

        public ConnectionContext Connection { get; }

        public async ValueTask<ProtocolReadResult<TReadMessage>> ReadAsync<TReadMessage>(IMessageReader<TReadMessage> reader, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (_hasMessage)
            {
                throw new InvalidOperationException("Advance must be called before calling ReadAsync");
            }

            var input = Connection.Transport.Input;

            while (true)
            {
                if (_isCanceled)
                {
                    break;
                }

                if (TryGetMessage(reader, _buffer, out var protocolMessage))
                {
                    _hasMessage = true;
                    return new ProtocolReadResult<TReadMessage>(protocolMessage, _isCanceled, isCompleted: false);
                }
                else if (_hasMessage)
                {
                    input.AdvanceTo(_consumed, _examined);
                }

                if (_isCompleted)
                {
                    if (!_buffer.IsEmpty)
                    {
                        throw new InvalidDataException("Connection terminated while reading a message.");
                    }

                    break;
                }

                var result = await input.ReadAsync(cancellationToken).ConfigureAwait(false);
                _buffer = result.Buffer;
                _isCanceled = result.IsCanceled;
                _isCompleted = result.IsCompleted;
                _consumed = _buffer.Start;
                _examined = _buffer.End;
            }

            return new ProtocolReadResult<TReadMessage>(default, _isCanceled, _isCompleted);
        }

        private bool TryGetMessage<TReadMessage>(IMessageReader<TReadMessage> reader, in ReadOnlySequence<byte> buffer, out TReadMessage protocolMessage)
        {
            // No message limit, just parse and dispatch
            if (_maximumMessageSize == null)
            {
                if (reader.TryParseMessage(buffer, out _consumed, out _examined, out protocolMessage))
                {
                    return true;
                }

                return false;
            }

            // We give the parser a sliding window of the default message size
            var maxMessageSize = _maximumMessageSize.Value;

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
                // REVIEW: Should this throw?
                return;
            }

            _buffer = _buffer.Slice(_consumed);

            if (_buffer.IsEmpty)
            {
                Connection.Transport.Input.AdvanceTo(_consumed, _examined);
            }

            _hasMessage = false;
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return default;
        }
    }
}
