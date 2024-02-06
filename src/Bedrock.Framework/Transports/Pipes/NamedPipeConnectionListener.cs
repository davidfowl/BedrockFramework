// TODO: Later see how much of this can be replaced with the following implementation:
// https://github.com/dotnet/aspnetcore/blob/main/src/Servers/Kestrel/Transport.NamedPipes/src/Internal/NamedPipeConnectionListener.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    public class NamedPipeConnectionListener : IConnectionListener
    {
        private readonly ILogger? _log;
        private readonly NamedPipeEndPoint _endpoint;
        private readonly CancellationTokenSource _listeningSource = new CancellationTokenSource();
        private readonly Channel<ConnectionContext> _acceptedQueue = Channel.CreateUnbounded<ConnectionContext>();
        private readonly Task _listeningTask;
        private readonly NamedPipeTransportOptions? _options;

        public NamedPipeConnectionListener(NamedPipeEndPoint endpoint)
        {
            _endpoint = endpoint;
            ListeningToken = _listeningSource.Token;
            _listeningTask = StartAsync();
        }

        public NamedPipeConnectionListener(EndPoint endpoint, NamedPipeTransportOptions options, ILoggerFactory logger)
        {
            _log = logger.CreateLogger<NamedPipeConnectionListener>();
            if (endpoint is not NamedPipeEndPoint namedPipeEndpoint) throw new ArgumentException("Must be of type NamedPipeEndPoint", nameof(endpoint));
            _endpoint = namedPipeEndpoint;
            _options = options;
            ListeningToken = _listeningSource.Token;
            _listeningTask = StartAsync();
        }

        public EndPoint EndPoint => _endpoint;

        public CancellationToken ListeningToken { get; }

        private async Task StartAsync()
        {
            while (true)
            {
                if (ListeningToken.IsCancellationRequested)
                {
                    // We're done listening
                    break;
                }

                NamedPipeServerStream stream;
                if (_options is null)
                {
                    stream = new NamedPipeServerStream(_endpoint.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, _endpoint.PipeOptions);
                }
                else
                {
                    stream = new NamedPipeServerStream(_endpoint.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, _endpoint.PipeOptions, _options.MaxReadBufferSize, _options.MaxWriteBufferSize);
                }

                try
                {
                    await stream.WaitForConnectionAsync(ListeningToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ListeningToken.IsCancellationRequested)
                {
                    // Cancelled the current token
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Listener disposed
                    break;
                }

                _acceptedQueue.Writer.TryWrite(new NamedPipeConnectionContext(stream, _endpoint));
            }

            _acceptedQueue.Writer.TryComplete();
        }

        public async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default)
        {
            while (await _acceptedQueue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_acceptedQueue.Reader.TryRead(out var connection))
                {
                    return connection;
                }
            }
            return null;
        }

        public ValueTask DisposeAsync()
        {
            _listeningSource.Dispose();
            return default;
        }

        public async ValueTask UnbindAsync(CancellationToken cancellationToken = default)
        {
            _listeningSource.Cancel();

            await _listeningTask.ConfigureAwait(false);
        }
    }
}
