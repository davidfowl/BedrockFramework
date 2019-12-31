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
        private bool _hasPreviousMessage = false;
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

            if (_hasPreviousMessage)
            {
                throw new InvalidOperationException("Advance must be called before calling ReadAsync");
            }

            var input = Connection.Transport.Input;

            TReadMessage protocolMessage = default;
            var isCanceled = false;
            var isCompleted = false;

            while (true)
            {
                var result = await input.ReadAsync(cancellationToken).ConfigureAwait(false);
                isCanceled = result.IsCanceled;
                isCompleted = result.IsCompleted;
                var buffer = result.Buffer;
                _consumed = buffer.Start;
                _examined = buffer.End;


                if (result.IsCanceled)
                {
                    break;
                }

                if (!buffer.IsEmpty)
                {
                    if (TryGetMessage(reader, buffer, out protocolMessage))
                    {
                        _hasPreviousMessage = true;
                        return new ProtocolReadResult<TReadMessage>(protocolMessage, isCanceled, isCompleted: false);
                    }
                    else
                    {
                        input.AdvanceTo(_consumed, _examined);
                    }
                }

                if (result.IsCompleted)
                {
                    if (!buffer.IsEmpty)
                    {
                        throw new InvalidDataException("Connection terminated while reading a message.");
                    }
                    break;
                }
            }

            return new ProtocolReadResult<TReadMessage>(protocolMessage, isCanceled, isCompleted);
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

            if (!_hasPreviousMessage)
            {
                // REVIEW: Should this throw?
                return;
            }

            Connection.Transport.Input.AdvanceTo(_consumed, _examined);

            _hasPreviousMessage = false;
        }

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return default;
        }
    }
}
