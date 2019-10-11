using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Bedrock.Framework.Protocols
{
    public class HubProtocol : IAsyncDisposable
    {
        private ConnectionContext _connection;
        private IHubProtocol _hubProtocol;
        private IInvocationBinder _invocationBinder;
        private int? _maximumMessageSize;
        private Task _runningTask;
        private Channel<HubMessage> _incoming = Channel.CreateBounded<HubMessage>(10);
        private Channel<HubMessage> _outgoing = Channel.CreateBounded<HubMessage>(10);

        private HubProtocol(ConnectionContext connection, int? maximumMessageSize, IHubProtocol hubProtocol, IInvocationBinder invocationBinder)
        {
            _connection = connection;
            _maximumMessageSize = maximumMessageSize;
            _hubProtocol = hubProtocol;
            _invocationBinder = invocationBinder;
            _runningTask = RunAsync();
        }

        public static HubProtocol CreateFromConnection(ConnectionContext connection, IHubProtocol hubProtocol, IInvocationBinder invocationBinder, int? maximumMessageSize = null)
        {
            return new HubProtocol(connection, maximumMessageSize, hubProtocol, invocationBinder);
        }

        public async ValueTask<HubMessage> ReadAsync(CancellationToken cancellationToken = default)
        {
            while (await _incoming.Reader.WaitToReadAsync())
            {
                if (_incoming.Reader.TryRead(out var hubMessage))
                {
                    return hubMessage;
                }
            }
            return null;
        }

        public virtual ValueTask WriteAsync(HubMessage message, CancellationToken cancellationToken = default)
        {
            return _outgoing.Writer.WriteAsync(message, cancellationToken);
        }

        private async Task WriteLoopAsync()
        {
            while (await _outgoing.Reader.WaitToReadAsync())
            {
                if (_outgoing.Reader.TryRead(out var hubMessage))
                {
                    _hubProtocol.WriteMessage(hubMessage, _connection.Transport.Output);
                    await _connection.Transport.Output.FlushAsync();
                }
            }
        }

        private async Task RunAsync()
        {
            // TODO: Uncomment this
            //if (!await ProcessHandshakeAsync())
            //{
            //    return;
            //}

            var writingTask = WriteLoopAsync();
            var readingTask = ReadLoopAsync();

            await readingTask;

            // Somebody called dispose
            _incoming.Writer.TryComplete();

            _outgoing.Writer.TryComplete();

            await writingTask;
        }


        private async Task ReadLoopAsync()
        {
            var input = _connection.Transport.Input;
            var protocol = _hubProtocol;

            while (true)
            {
                var result = await input.ReadAsync();
                var buffer = result.Buffer;

                try
                {
                    if (result.IsCanceled)
                    {
                        break;
                    }

                    if (!buffer.IsEmpty)
                    {
                        // _connection.ResetClientTimeout();

                        // No message limit, just parse and dispatch
                        if (_maximumMessageSize == null)
                        {
                            while (protocol.TryParseMessage(ref buffer, _invocationBinder, out var message))
                            {
                                await _incoming.Writer.WriteAsync(message);
                            }
                        }
                        else
                        {
                            // We give the parser a sliding window of the default message size
                            var maxMessageSize = _maximumMessageSize.Value;

                            while (!buffer.IsEmpty)
                            {
                                var segment = buffer;
                                var overLength = false;

                                if (segment.Length > maxMessageSize)
                                {
                                    segment = segment.Slice(segment.Start, maxMessageSize);
                                    overLength = true;
                                }

                                if (protocol.TryParseMessage(ref segment, _invocationBinder, out var message))
                                {
                                    await _incoming.Writer.WriteAsync(message);
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

                                // Update the buffer to the remaining segment
                                buffer = buffer.Slice(segment.Start);
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
                    input.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }

        private async Task<bool> ProcessHandshakeAsync()
        {
            // The protocol should be resolved from here
            while (true)
            {
                var result = await _connection.Transport.Input.ReadAsync();
                var buffer = result.Buffer;
                var consumed = buffer.Start;
                var examined = buffer.End;
                try
                {
                    if (HandshakeProtocol.TryParseRequestMessage(ref buffer, out _))
                    {
                        // We parsed the handshake
                        consumed = buffer.Start;
                        examined = consumed;
                        break;
                    }

                    if (result.IsCompleted)
                    {
                        // The connection closed before we were able to parse the handshake
                        // Don't expose it as an accepted connection
                        return false;
                    }
                }
                finally
                {
                    _connection.Transport.Input.AdvanceTo(consumed, examined);
                }
            }

            return true;
        }

        public async ValueTask DisposeAsync()
        {
            _connection.Transport.Input.CancelPendingRead();

            await _runningTask;
        }
    }
}
