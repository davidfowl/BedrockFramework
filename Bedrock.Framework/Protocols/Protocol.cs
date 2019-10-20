using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
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
        private readonly Channel<TReadMessage> _input = Channel.CreateBounded<TReadMessage>(100);
        private readonly Channel<TWriteMessage> _output = Channel.CreateBounded<TWriteMessage>(100);
        private Task _readerLoop;
        private Task _writerLoop;

        public Protocol(ConnectionContext connection, TReader reader, TWriter writer, int? maximumMessageSize)
        {
            Connection = connection;
            _reader = reader;
            _writer = writer;
            _maximumMessageSize = maximumMessageSize;
        }

        public ConnectionContext Connection { get; }

        public ValueTask<TReadMessage> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (_readerLoop == null) 
            {
                _readerLoop = Task.Run(ReadLoop);
            }

            return _input.Reader.ReadAsync(cancellationToken);
        }

        private async Task ReadLoop() 
        {
            var input = Connection.Transport.Input;
            var reader = _reader;
            var inputBuffer = _input.Writer;
            
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
                            while (reader.TryParseMessage(buffer, out consumed, out examined, out var protocolMessage))
                            {
                                await inputBuffer.WriteAsync(protocolMessage);
                                buffer = buffer.Slice(consumed);
                            }
                        }
                        else
                        {
                            // We give the parser a sliding window of the default message size
                            var maxMessageSize = _maximumMessageSize.Value;
                            if (buffer.Length > maxMessageSize)
                            {
                                inputBuffer.Complete(new InvalidDataException($"The maximum message size of {maxMessageSize}B was exceeded. The message size can be configured in AddHubOptions."));
                            }

                            while (reader.TryParseMessage(buffer, out consumed, out examined, out var protocolMessage))
                            {
                                await inputBuffer.WriteAsync(protocolMessage);
                                buffer = buffer.Slice(consumed);
                            }
                        }
                    }

                    if (result.IsCompleted)
                    {
                        if (!buffer.IsEmpty)
                        {
                            inputBuffer.Complete(new InvalidDataException("Connection terminated while reading a message."));
                        }
                        else
                        {
                            inputBuffer.Complete();
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
        }

        public ValueTask WriteAsync(TWriteMessage protocolMessage, CancellationToken cancellationToken = default)
        {
            if (_writerLoop == null)
            {
                _writerLoop = Task.Run(WriteLoop);
            }

            return _output.Writer.WriteAsync(protocolMessage, cancellationToken);            
        }

        private async Task WriteLoop()
        {
            var outputBuffer = _output.Reader;
            var output = Connection.Transport.Output;

            while (await outputBuffer.WaitToReadAsync())
            {
                while (outputBuffer.TryRead(out var protocolMessage))
                {
                    _writer.WriteMessage(protocolMessage, output);
                }
                await output.FlushAsync();
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
