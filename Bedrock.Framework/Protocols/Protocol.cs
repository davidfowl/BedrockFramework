using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class Protocol<TReader, TWriter> where TReader : IProtocolReader
                                            where TWriter : IProtocolWriter
    {
        private readonly ConnectionContext _connection;
        private readonly TReader _reader;
        private readonly TWriter _writer;
        private readonly int? _maximumMessageSize;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        public Protocol(ConnectionContext connection, TReader reader, TWriter writer, int? maximumMessageSize)
        {
            _connection = connection;
            _reader = reader;
            _writer = writer;
            _maximumMessageSize = maximumMessageSize;
        }

        public async ValueTask<ProtocolMessage> ReadAsync(CancellationToken cancellationToken = default)
        {
            var input = _connection.Transport.Input;
            var reader = _reader;

            ProtocolMessage protocolMessage = null;

            while (true)
            {
                var result = await input.ReadAsync();
                var buffer = result.Buffer;
                var consumed = buffer.Start;
                var examined = buffer.End;

                try
                {
                    if (result.IsCanceled)
                    {
                        break;
                    }

                    if (!buffer.IsEmpty)
                    {
                        // No message limit, just parse and dispatch
                        if (_maximumMessageSize == null)
                        {
                            if (reader.TryParseMessage(ref buffer, out protocolMessage))
                            {
                                consumed = buffer.Start;
                                examined = consumed;
                                break;
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

                                if (reader.TryParseMessage(ref segment, out protocolMessage))
                                {
                                    consumed = buffer.Start;
                                    examined = consumed;
                                    break;
                                }
                                else if (overLength)
                                {
                                    throw new InvalidDataException($"The maximum message size of {maxMessageSize}B was exceeded. The message size can be configured in AddHubOptions.");
                                }
                                else
                                {
                                    // No need to update the buffer since we didn't parse anything
                                    break;
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
                finally
                {
                    // The buffer was sliced up to where it was consumed, so we can just advance to the start.
                    // We mark examined as buffer.End so that if we didn't receive a full frame, we'll wait for more data
                    // before yielding the read again.
                    input.AdvanceTo(consumed, examined);
                }
            }

            return protocolMessage;
        }

        public async ValueTask WriteAsync(ProtocolMessage protocolMessage, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                _writer.WriteMessage(protocolMessage, _connection.Transport.Output);
                await _connection.Transport.Output.FlushAsync(cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static Protocol<TReader, TWriter> Create(ConnectionContext connection, TReader reader, TWriter writer, int? maxMessageSize = null)
        {
            return new Protocol<TReader, TWriter>(connection, reader, writer, maxMessageSize);
        }
    }

    public class ProtocolMessage
    {

    }

    public interface IProtocolReader
    {
        bool TryParseMessage(ref ReadOnlySequence<byte> input, out ProtocolMessage message);
    }

    public interface IProtocolWriter
    {
        void WriteMessage(ProtocolMessage message, IBufferWriter<byte> output);
    }
}
