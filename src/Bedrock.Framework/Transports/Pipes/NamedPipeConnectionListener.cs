using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public class NamedPipeConnectionListener : IConnectionListener
    {
        private readonly NamedPipeEndPoint _endpoint;
        private readonly CancellationTokenSource _listeningSource = new CancellationTokenSource();
        private readonly Channel<ConnectionContext> _acceptedQueue = Channel.CreateUnbounded<ConnectionContext>();
        private readonly Task _listeningTask;

        public NamedPipeConnectionListener(NamedPipeEndPoint endpoint)
        {
            _endpoint = endpoint;
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

                var stream = new NamedPipeServerStream(_endpoint.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, _endpoint.PipeOptions);

                try
                {
                    await stream.WaitForConnectionAsync(ListeningToken);
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

                _acceptedQueue.Writer.TryWrite(new NamedPipeConnectionContext(stream));
            }

            _acceptedQueue.Writer.TryComplete();
        }

        public async ValueTask<ConnectionContext> AcceptAsync(CancellationToken cancellationToken = default)
        {
            while (await _acceptedQueue.Reader.WaitToReadAsync(cancellationToken))
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

            await _listeningTask;
        }
    }
}
