using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public static class Protocol 
    {
        public static ProtocolWriter<TWriteMessage> CreateWriter<TWriteMessage>(this ConnectionContext connection, IProtocolWriter<TWriteMessage> writer)
            => new ProtocolWriter<TWriteMessage>(connection, writer);
        public static ProtocolWriter<TWriteMessage> CreateWriter<TWriteMessage>(this ConnectionContext connection, IProtocolWriter<TWriteMessage> writer, SemaphoreSlim semaphore)
            => new ProtocolWriter<TWriteMessage>(connection, writer, semaphore);

        public static ProtocolReader<TReadMessage> CreateReader<TReadMessage>(this ConnectionContext connection, IProtocolReader<TReadMessage> reader, int? maximumMessageSize = null)
            => new ProtocolReader<TReadMessage>(connection, reader, maximumMessageSize);
    }

    public class ProtocolWriter<TWriteMessage>
    {
        private readonly IProtocolWriter<TWriteMessage> _writer;
        private readonly SemaphoreSlim _semaphore;

        public ProtocolWriter(ConnectionContext connection, IProtocolWriter<TWriteMessage> writer)
            : this(connection, writer, new SemaphoreSlim(1))
        {
        }

        public ProtocolWriter(ConnectionContext connection, IProtocolWriter<TWriteMessage> writer, SemaphoreSlim semaphore)
        {
            Connection = connection;
            _writer = writer;
            _semaphore = semaphore;
        }

        public ConnectionContext Connection { get; }
               
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

        public async ValueTask WriteManyAsync(IEnumerable<TWriteMessage> protocolMessages, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                foreach (var protocolMessage in protocolMessages)
                {
                    _writer.WriteMessage(protocolMessage, Connection.Transport.Output);
                }

                await Connection.Transport.Output.FlushAsync(cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    public class ProtocolReader<TReadMessage> 
    {
        private readonly IProtocolReader<TReadMessage> _reader;
        private readonly int? _maximumMessageSize;

        public ProtocolReader(ConnectionContext connection, IProtocolReader<TReadMessage> reader, int? maximumMessageSize)
        {
            Connection = connection;
            _reader = reader;
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
