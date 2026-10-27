using System;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// Exposes a <see cref="WebSocketSession"/> as a byte-stream <see cref="IDuplexPipe"/>, so any Bedrock protocol can run
/// over WebSockets. Incoming message payloads are written to <see cref="Input"/> back to back, and data flushed to
/// <see cref="Output"/> is sent as WebSocket messages. Message boundaries are not preserved in either direction (flushes
/// made in quick succession may be sent as one message), so use a self-delimiting protocol such as a length prefix.
/// Completing <see cref="Output"/> performs the close handshake.
/// </summary>
public sealed class WebSocketDuplexPipe : IDuplexPipe, IAsyncDisposable
{
    private readonly WebSocketSession _session;
    private readonly WebSocketMessageType _messageType;
    private readonly Pipe _incoming;
    private readonly Pipe _outgoing;
    private readonly Task _receiveTask;
    private readonly Task _sendTask;

    public WebSocketDuplexPipe(WebSocketSession session, WebSocketMessageType messageType = WebSocketMessageType.Binary, PipeOptions pipeOptions = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (messageType == WebSocketMessageType.Close)
        {
            throw new ArgumentOutOfRangeException(nameof(messageType));
        }

        _messageType = messageType;
        pipeOptions ??= new PipeOptions(useSynchronizationContext: false);
        _incoming = new Pipe(pipeOptions);
        _outgoing = new Pipe(pipeOptions);

        _receiveTask = ReceiveLoopAsync();
        _sendTask = SendLoopAsync();
    }

    /// <summary>
    /// The session being adapted.
    /// </summary>
    public WebSocketSession Session => _session;

    /// <summary>
    /// The concatenated payloads of received messages. Completes when the peer closes the session.
    /// </summary>
    public PipeReader Input => _incoming.Reader;

    /// <summary>
    /// Flushed data is sent as WebSocket messages; buffered data from several flushes may be combined into one.
    /// Complete it to close the session.
    /// </summary>
    public PipeWriter Output => _outgoing.Writer;

    /// <summary>
    /// Completes when both directions have finished.
    /// </summary>
    public Task Completion => Task.WhenAll(_receiveTask, _sendTask);

    private async Task ReceiveLoopAsync()
    {
        await Task.Yield();
        var writer = _incoming.Writer;
        Exception error = null;

        byte[] discard = null;

        try
        {
            while (true)
            {
                if (discard != null)
                {
                    // The application stopped reading; keep reading so control frames (and the peer's Close) are still processed
                    var discarded = await _session.ReadAsync(discard).ConfigureAwait(false);
                    if (discarded.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    continue;
                }

                var memory = writer.GetMemory(4096);
                var result = await _session.ReadAsync(memory).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                writer.Advance(result.Count);
                var flush = await writer.FlushAsync().ConfigureAwait(false);
                if (flush.IsCompleted || flush.IsCanceled)
                {
                    discard = new byte[4096];
                }
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            await writer.CompleteAsync(error).ConfigureAwait(false);
        }
    }

    private async Task SendLoopAsync()
    {
        await Task.Yield();
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
                    if (!buffer.IsEmpty && _session.State == WebSocketState.Open)
                    {
                        await _session.SendAsync(buffer, _messageType, endOfMessage: true).ConfigureAwait(false);
                    }
                }
                finally
                {
                    reader.AdvanceTo(buffer.End);
                }

                if (result.IsCompleted || result.IsCanceled || _session.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
                {
                    break;
                }
            }

            // Only send the Close frame here: the receive loop is the session's single reader and observes the peer's reply
            if (_session.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _session.CloseOutputAsync(WebSocketCloseStatus.NormalClosure).ConfigureAwait(false);
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

    /// <summary>
    /// Closes the session (if still open), waits for the close handshake to finish and disposes the session.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // The application is done with both directions
        await _outgoing.Writer.CompleteAsync().ConfigureAwait(false);
        await _incoming.Reader.CompleteAsync().ConfigureAwait(false);

        try
        {
            await Completion.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }
        catch
        {
            // Timed out waiting for the peer, or one of the loops failed; abort below
        }

        if (_session.State != WebSocketState.Closed)
        {
            _session.Abort();
        }

        try
        {
            await Completion.ConfigureAwait(false);
        }
        catch
        {
        }

        await _session.DisposeAsync().ConfigureAwait(false);
    }
}
