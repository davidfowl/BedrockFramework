using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public class NamedPipeConnectionListener : ConnectionListener, IConnectionProperties
    {
        private readonly NamedPipeEndPoint _endpoint;
        private readonly IConnectionProperties _properties;
        private readonly CancellationTokenSource _listeningSource = new CancellationTokenSource();
        private readonly Channel<Connection> _acceptedQueue = Channel.CreateUnbounded<Connection>();
        private readonly Task _listeningTask;

        public NamedPipeConnectionListener(NamedPipeEndPoint endpoint, IConnectionProperties properties)
        {
            _endpoint = endpoint;
            _properties = properties;
            ListeningToken = _listeningSource.Token;
            _listeningTask = StartAsync();
        }

        public EndPoint EndPoint => _endpoint;

        public CancellationToken ListeningToken { get; }

        public override IConnectionProperties ListenerProperties => this;

        public override EndPoint LocalEndPoint => _endpoint;

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

                _acceptedQueue.Writer.TryWrite(Connection.FromStream(stream, leaveOpen: false));
            }

            _acceptedQueue.Writer.TryComplete();
        }

        public async ValueTask<Connection> AcceptAsync(CancellationToken cancellationToken = default)
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

        protected override ValueTask DisposeAsyncCore()
        {
            _listeningSource.Dispose();
            return base.DisposeAsyncCore();
        }

        public override ValueTask<Connection> AcceptAsync(IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            return _properties.TryGet(propertyKey, out property);
        }
    }
}
