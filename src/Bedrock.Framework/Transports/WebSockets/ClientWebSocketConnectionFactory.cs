using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Bedrock.Framework;

/// <summary>
/// Exposes the <see cref="System.Net.WebSockets.ClientWebSocket"/> behind a connection created by
/// <see cref="ClientWebSocketConnectionFactory"/>, for example to read the negotiated subprotocol or close status.
/// </summary>
public interface IClientWebSocketFeature
{
    ClientWebSocket WebSocket { get; }
}

/// <summary>
/// A Bedrock transport over <see cref="ClientWebSocket"/>. This is the transport to use in the browser (Blazor
/// WebAssembly), where raw sockets are unavailable and the browser performs the WebSocket protocol itself; it works
/// on every other platform too.
/// </summary>
/// <remarks>
/// Connect with a <see cref="UriEndPoint"/> (ws:// or wss://). The connection's transport is a byte stream: data flushed
/// to <c>Transport.Output</c> is sent as WebSocket messages (flushes made in quick succession may be combined into one),
/// and received message payloads are written to <c>Transport.Input</c> back to back. Message boundaries are not
/// preserved, so length-prefixed or otherwise self-delimiting Bedrock protocols run unchanged.
/// Completing the output performs the close handshake.
/// </remarks>
public sealed class ClientWebSocketConnectionFactory : IConnectionFactory
{
    private readonly Action<ClientWebSocketOptions> _configure;
    private readonly WebSocketMessageType _messageType;

    /// <param name="configure">Configures each socket before connecting (subprotocols, headers, keep-alive; some options are unsupported in the browser).</param>
    /// <param name="messageType">The message type used for outgoing data.</param>
    public ClientWebSocketConnectionFactory(Action<ClientWebSocketOptions> configure = null, WebSocketMessageType messageType = WebSocketMessageType.Binary)
    {
        if (messageType == WebSocketMessageType.Close)
        {
            throw new ArgumentOutOfRangeException(nameof(messageType));
        }

        _configure = configure;
        _messageType = messageType;
    }

    public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
    {
        if (endpoint is not UriEndPoint uriEndPoint)
        {
            throw new NotSupportedException($"{endpoint} is not supported. Use a UriEndPoint with a ws:// or wss:// URI.");
        }

        var socket = new ClientWebSocket();
        try
        {
            _configure?.Invoke(socket.Options);
            await socket.ConnectAsync(uriEndPoint.Uri, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return new ClientWebSocketConnection(socket, uriEndPoint, _messageType);
    }

    private sealed class ClientWebSocketConnection : ConnectionContext, IClientWebSocketFeature, IConnectionLifetimeFeature, IConnectionIdFeature, IConnectionItemsFeature, IConnectionEndPointFeature
    {
        private readonly ClientWebSocket _socket;
        private readonly WebSocketMessageType _messageType;
        private readonly Pipe _incoming;
        private readonly Pipe _outgoing;
        private readonly CancellationTokenSource _closed = new();
        private readonly Task _receiveTask;
        private readonly Task _sendTask;
        private int _disposed;

        public ClientWebSocketConnection(ClientWebSocket socket, UriEndPoint endpoint, WebSocketMessageType messageType)
        {
            _socket = socket;
            _messageType = messageType;

            var options = new PipeOptions(useSynchronizationContext: false);
            _incoming = new Pipe(options);
            _outgoing = new Pipe(options);

            Transport = new DuplexPipe(_incoming.Reader, _outgoing.Writer);
            RemoteEndPoint = endpoint;
            ConnectionClosed = _closed.Token;

            Features.Set<IClientWebSocketFeature>(this);
            Features.Set<IConnectionLifetimeFeature>(this);
            Features.Set<IConnectionIdFeature>(this);
            Features.Set<IConnectionItemsFeature>(this);
            Features.Set<IConnectionEndPointFeature>(this);

            _receiveTask = ReceiveLoopAsync();
            _sendTask = SendLoopAsync();
        }

        public ClientWebSocket WebSocket => _socket;

        public override string ConnectionId { get; set; } = Guid.NewGuid().ToString("N");
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems();
        public override IDuplexPipe Transport { get; set; }
        public override EndPoint RemoteEndPoint { get; set; }
        public override EndPoint LocalEndPoint { get; set; }
        public override CancellationToken ConnectionClosed { get; set; }

        private async Task ReceiveLoopAsync()
        {
            var writer = _incoming.Writer;
            Exception error = null;
            try
            {
                while (true)
                {
                    var memory = writer.GetMemory(4096);
                    var result = await _socket.ReceiveAsync(memory, CancellationToken.None).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // Complete the handshake if the server initiated it
                        if (_socket.State == WebSocketState.CloseReceived)
                        {
                            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                        }
                        break;
                    }

                    writer.Advance(result.Count);
                    var flush = await writer.FlushAsync().ConfigureAwait(false);
                    if (flush.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                await writer.CompleteAsync(error is WebSocketException or OperationCanceledException ? null : error).ConfigureAwait(false);
                _closed.Cancel();
            }
        }

        private async Task SendLoopAsync()
        {
            var reader = _outgoing.Reader;
            Exception error = null;
            try
            {
                while (true)
                {
                    var result = await reader.ReadAsync().ConfigureAwait(false);
                    var buffer = result.Buffer;
                    try
                    {
                        if (!buffer.IsEmpty && _socket.State == WebSocketState.Open)
                        {
                            await SendMessageAsync(buffer).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        reader.AdvanceTo(buffer.End);
                    }

                    if (result.IsCompleted || result.IsCanceled || _socket.State != WebSocketState.Open)
                    {
                        break;
                    }
                }

                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                await reader.CompleteAsync(error).ConfigureAwait(false);
            }
        }

        private async ValueTask SendMessageAsync(ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsSingleSegment)
            {
                await _socket.SendAsync(buffer.First, _messageType, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            // Send every segment as a fragment of one message
            var position = buffer.Start;
            buffer.TryGet(ref position, out var segment);
            while (true)
            {
                var hasNext = buffer.TryGet(ref position, out var next);
                await _socket.SendAsync(segment, _messageType, endOfMessage: !hasNext, CancellationToken.None).ConfigureAwait(false);
                if (!hasNext)
                {
                    break;
                }
                segment = next;
            }
        }

        public override void Abort(ConnectionAbortedException abortReason)
        {
            _socket.Abort();
            _outgoing.Reader.CancelPendingRead();
        }

        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            // The application is done with both directions
            await _outgoing.Writer.CompleteAsync().ConfigureAwait(false);
            await _incoming.Reader.CompleteAsync().ConfigureAwait(false);

            try
            {
                await Task.WhenAll(_receiveTask, _sendTask).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch
            {
                _socket.Abort();
            }

            _socket.Dispose();
            _closed.Cancel();
            _closed.Dispose();
        }
    }
}

public static class ClientWebSocketClientBuilderExtensions
{
    /// <summary>
    /// Uses <see cref="ClientWebSocket"/> as the transport. Connect with a <see cref="UriEndPoint"/>.
    /// This is the transport for Blazor WebAssembly and other browser environments.
    /// </summary>
    public static ClientBuilder UseClientWebSocket(this ClientBuilder builder, Action<ClientWebSocketOptions> configure = null, WebSocketMessageType messageType = WebSocketMessageType.Binary)
        => builder.UseConnectionFactory(new ClientWebSocketConnectionFactory(configure, messageType));
}
