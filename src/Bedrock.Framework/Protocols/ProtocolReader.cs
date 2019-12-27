﻿using System;
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

        public async ValueTask<ReadResult<TReadMessage>> ReadAsync<TReadMessage>(IMessageReader<TReadMessage> reader,  CancellationToken cancellationToken = default)
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
                var result = await input.ReadAsync(cancellationToken);
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
                    // No message limit, just parse and dispatch
                    if (_maximumMessageSize == null)
                    {
                        if (reader.TryParseMessage(buffer, out _consumed, out _examined, out protocolMessage))
                        {
                            var message = new ReadResult<TReadMessage>(protocolMessage, isCanceled, isCompleted: false);
                            _hasPreviousMessage = true;
                            return message;
                        }
                        else
                        {
                            // No message so advance
                            input.AdvanceTo(_consumed, _examined);
                        }
                    }
                    else
                    {
                        // We give the parser a sliding window of the default message size
                        var maxMessageSize = _maximumMessageSize.Value;

                        if (!buffer.IsEmpty)
                        {
                            var segment = buffer;
                            var overLength = false;

                            if (segment.Length > maxMessageSize)
                            {
                                segment = segment.Slice(segment.Start, maxMessageSize);
                                overLength = true;
                            }

                            if (reader.TryParseMessage(segment, out _consumed, out _examined, out protocolMessage))
                            {
                                var message = new ReadResult<TReadMessage>(protocolMessage, isCanceled, isCompleted: false);
                                _hasPreviousMessage = true;
                                return message;
                            }
                            else if (overLength)
                            {
                                throw new InvalidDataException($"The maximum message size of {maxMessageSize}B was exceeded. The message size can be configured in AddHubOptions.");
                            }
                            else
                            {
                                input.AdvanceTo(_consumed, _examined);
                                // No need to update the buffer since we didn't parse anything
                                continue;
                            }
                        }
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

            return new ReadResult<TReadMessage>(protocolMessage, isCanceled, isCompleted);
        }

        public void Advance()
        {
            // TODO: More error handling here
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
