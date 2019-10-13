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
    public class Protocol<TReader, TWriter, TReadMessage, TWriteMessage> where TReader : IProtocolReader<TReadMessage>
                                                                         where TWriter : IProtocolWriter<TWriteMessage>
    {
        private readonly TReader _reader;
        private readonly TWriter _writer;
        private readonly int? _maximumMessageSize;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        public Protocol(ConnectionContext connection, TReader reader, TWriter writer, int? maximumMessageSize)
        {
            Connection = connection;
            _reader = reader;
            _writer = writer;
            _maximumMessageSize = maximumMessageSize;
        }

        public ConnectionContext Connection { get; }

        public async ValueTask<TReadMessage> ReadAsync(CancellationToken cancellationToken = default)
        {
            var input = Connection.Transport.Input;
            var reader = _reader;

            TReadMessage protocolMessage = default;

            while (true)
            {
                var result = await input.ReadAsync(cancellationToken);
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
                            if (reader.TryParseMessage(buffer, out consumed, out examined, out protocolMessage))
                            {
                                return protocolMessage;
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

                                if (reader.TryParseMessage(segment, out consumed, out examined, out protocolMessage))
                                {
                                    return protocolMessage;
                                }
                                else if (overLength)
                                {
                                    throw new InvalidDataException($"The maximum message size of {maxMessageSize}B was exceeded. The message size can be configured in AddHubOptions.");
                                }
                                else
                                {
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

        public async ValueTask WriteAsync(TWriteMessage protocolMessage, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                _writer.WriteMessage(protocolMessage, Connection.Transport.Output);
                await Connection.Transport.Output.FlushAsync(cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static Protocol<TReader, TWriter, TReadMessage, TWriteMessage> Create(ConnectionContext connection, TReader reader, TWriter writer, int? maxMessageSize = null)
        {
            return new Protocol<TReader, TWriter, TReadMessage, TWriteMessage>(connection, reader, writer, maxMessageSize);
        }
    }

    public interface IProtocolReader<TMessage>
    {
        bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out TMessage message);
    }

    public interface IProtocolWriter<TMessage>
    {
        void WriteMessage(TMessage message, IBufferWriter<byte> output);
    }
}
