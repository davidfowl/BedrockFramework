using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// Opens WebSocket sessions over HTTP/2 using extended CONNECT (RFC 8441), via <see cref="SocketsHttpHandler"/>.
/// </summary>
/// <remarks>
/// Pass a shared <see cref="HttpMessageInvoker"/> to multiplex many WebSockets over one HTTP/2 connection. To route the
/// underlying connection through a Bedrock client pipeline, set <see cref="SocketsHttpHandler.ConnectCallback"/> on the
/// handler and return the connection's transport as a stream.
/// </remarks>
public static class WebSocketHttp2Client
{
    /// <summary>
    /// Connects to <paramref name="uri"/> (ws:// maps to h2c with prior knowledge, wss:// to HTTP/2 over TLS).
    /// </summary>
    /// <param name="uri">The ws://, wss://, http:// or https:// URI.</param>
    /// <param name="options">Client options.</param>
    /// <param name="invoker">The HTTP invoker to use. When null a private <see cref="SocketsHttpHandler"/> is created and
    /// disposed with the session.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <exception cref="WebSocketHandshakeException">The server rejected the extended CONNECT or answered it improperly.</exception>
    public static async ValueTask<WebSocketSession> ConnectAsync(Uri uri, WebSocketClientOptions options = null, HttpMessageInvoker invoker = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        options ??= new WebSocketClientOptions();
        options.Validate();

        var httpUri = uri.Scheme switch
        {
            "ws" => new UriBuilder(uri) { Scheme = Uri.UriSchemeHttp }.Uri,
            "wss" => new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps }.Uri,
            "http" or "https" => uri,
            _ => throw new ArgumentException("The URI must be an absolute ws://, wss://, http:// or https:// URI.", nameof(uri))
        };

        var ownsInvoker = invoker == null;
        invoker ??= new HttpMessageInvoker(new SocketsHttpHandler(), disposeHandler: true);

        HttpResponseMessage response = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Connect, httpUri)
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            };

            // RFC 8441 4: the :protocol pseudo-header carries the upgrade token
            request.Headers.Protocol = "websocket";
            foreach (var (name, value) in WebSocketClientProtocol.GetCommonRequestHeaders(options))
            {
                if (!request.Headers.TryAddWithoutValidation(name, value))
                {
                    throw new ArgumentException($"The header '{name}' cannot be sent on an HTTP/2 extended CONNECT request.", nameof(options));
                }
            }

            try
            {
                response = await invoker.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new WebSocketHandshakeException(null, $"The HTTP/2 extended CONNECT request failed: {ex.Message}", null) { };
            }

            var headers = ToDictionary(response);
            var status = (int)response.StatusCode;
            if (status != 200)
            {
                throw new WebSocketHandshakeException(status, $"The server rejected the WebSocket extended CONNECT with status {status}.", headers);
            }

            var (subProtocol, compression) = WebSocketClientProtocol.ValidateNegotiation(status, headers, options);

            // For an extended CONNECT the response content is the bidirectional tunnel
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var transport = new StreamDuplexPipe(stream);
            var owner = new Owner(transport, response, ownsInvoker ? invoker : null);
            response = null;

            return WebSocketSession.Create(transport, connection: null, isServer: false, options, subProtocol, compression, owner);
        }
        catch
        {
            response?.Dispose();
            if (ownsInvoker)
            {
                invoker.Dispose();
            }
            throw;
        }
    }

    private static Dictionary<string, StringValues> ToDictionary(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            headers[header.Key] = new StringValues(header.Value.ToArray());
        }
        return headers;
    }

    private sealed class Owner(StreamDuplexPipe transport, HttpResponseMessage response, HttpMessageInvoker invoker) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transport.DisposeAsync().ConfigureAwait(false);
            response.Dispose();
            invoker?.Dispose();
        }
    }
}

/// <summary>
/// A duplex pipe over a bidirectional stream (an HTTP/2 tunnel, for example).
/// </summary>
internal sealed class StreamDuplexPipe : IDuplexPipe, IAsyncDisposable
{
    private readonly Stream _stream;

    public StreamDuplexPipe(Stream stream)
    {
        _stream = stream;
        Input = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
        Output = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
    }

    public PipeReader Input { get; }

    public PipeWriter Output { get; }

    public async ValueTask DisposeAsync()
    {
        await Input.CompleteAsync().ConfigureAwait(false);
        await Output.CompleteAsync().ConfigureAwait(false);
        await _stream.DisposeAsync().ConfigureAwait(false);
    }
}
