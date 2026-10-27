using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// The server side of the WebSocket opening handshake: RFC 6455 over an HTTP/1.1 connection, or, when the connection
/// carries an <see cref="IWebSocketUpgradeFeature"/>, an upgrade already parsed by an HTTP host (including RFC 8441
/// extended CONNECT over HTTP/2).
/// </summary>
/// <example>
/// <code>
/// var handshake = new WebSocketServerProtocol(connection, options);
/// var request = await handshake.ReadRequestAsync();
/// if (request.Path != "/chat") { await handshake.RejectAsync(404); return; }
/// await using var session = await handshake.AcceptAsync();
/// </code>
/// </example>
public sealed class WebSocketServerProtocol
{
    private readonly ConnectionContext _connection;
    private readonly WebSocketServerOptions _options;
    private readonly IWebSocketUpgradeFeature _upgradeFeature;
    private bool _responded;

    public WebSocketServerProtocol(ConnectionContext connection, WebSocketServerOptions options = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? new WebSocketServerOptions();
        _options.Validate();
        _upgradeFeature = connection.Features?.Get<IWebSocketUpgradeFeature>();
    }

    /// <summary>
    /// The validated upgrade request, once <see cref="ReadRequestAsync"/> has completed.
    /// </summary>
    public WebSocketUpgradeRequest Request { get; private set; }

    /// <summary>
    /// Reads and validates the upgrade request. Invalid requests are answered with the appropriate HTTP error
    /// (400, 403, 405, 426 or 431) before a <see cref="WebSocketHandshakeException"/> is thrown.
    /// </summary>
    public async ValueTask<WebSocketUpgradeRequest> ReadRequestAsync(CancellationToken cancellationToken = default)
    {
        if (Request != null)
        {
            return Request;
        }

        var isExtendedConnect = _upgradeFeature?.IsExtendedConnect ?? false;
        try
        {
            HttpHead head;
            if (_upgradeFeature != null)
            {
                head = new HttpHead { First = _upgradeFeature.Method, Second = _upgradeFeature.RequestTarget, Third = _upgradeFeature.HttpVersion };
                foreach (var header in _upgradeFeature.Headers)
                {
                    head.Headers[header.Key] = header.Value;
                }
            }
            else
            {
                head = await HttpHead.ReadAsync(_connection.Transport.Input, isRequest: true, _options.MaxHandshakeSize, cancellationToken).ConfigureAwait(false)
                    ?? throw new WebSocketHandshakeException(null, "The connection was closed before an upgrade request was received.");
            }

            Request = isExtendedConnect ? ValidateExtendedConnect(head) : ValidateUpgrade(head);
            return Request;
        }
        catch (WebSocketHandshakeException ex) when (ex.StatusCode is int status)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (status == 426 || (isExtendedConnect && ex.Data.Contains(UnsupportedVersionMarker)))
            {
                headers[WebSocketHandshake.HeaderVersion] = WebSocketHandshake.SupportedVersion;
            }
            if (status == 405)
            {
                headers["Allow"] = isExtendedConnect ? "CONNECT" : "GET";
            }

            await SendErrorAsync(status, headers, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private const string UnsupportedVersionMarker = "UnsupportedVersion";

    /// <summary>
    /// RFC 6455 section 4.2.1.
    /// </summary>
    private WebSocketUpgradeRequest ValidateUpgrade(HttpHead head)
    {
        if (!string.Equals(head.Third, "HTTP/1.1", StringComparison.Ordinal))
        {
            throw new WebSocketHandshakeException(400, $"WebSocket upgrades require HTTP/1.1, but the request used '{head.Third}'.");
        }

        if (!string.Equals(head.First, "GET", StringComparison.Ordinal))
        {
            throw new WebSocketHandshakeException(405, $"WebSocket upgrades require the GET method, but the request used '{head.First}'.");
        }

        if (head[WebSocketHandshake.HeaderHost].Count != 1)
        {
            throw new WebSocketHandshakeException(400, "The upgrade request must contain exactly one Host header.");
        }

        if (!WebSocketHandshake.ContainsToken(head[WebSocketHandshake.HeaderUpgrade], "websocket"))
        {
            throw new WebSocketHandshakeException(400, "The request is not a WebSocket upgrade request (missing 'Upgrade: websocket').");
        }

        if (!WebSocketHandshake.ContainsToken(head[WebSocketHandshake.HeaderConnection], "Upgrade"))
        {
            throw new WebSocketHandshakeException(400, "The upgrade request is missing 'Connection: Upgrade'.");
        }

        ValidateVersion(head, status: 426);

        var key = head[WebSocketHandshake.HeaderKey];
        if (key.Count != 1 || !WebSocketHandshake.IsValidKey(key.ToString()))
        {
            throw new WebSocketHandshakeException(400, "The upgrade request has a missing or invalid Sec-WebSocket-Key.");
        }

        return CreateRequest(head, key.ToString().Trim());
    }

    /// <summary>
    /// RFC 8441 sections 4 and 5: an extended CONNECT with :protocol = websocket. There is no Sec-WebSocket-Key.
    /// </summary>
    private WebSocketUpgradeRequest ValidateExtendedConnect(HttpHead head)
    {
        if (!string.Equals(head.First, "CONNECT", StringComparison.Ordinal))
        {
            throw new WebSocketHandshakeException(405, $"WebSockets over HTTP/2 require the CONNECT method, but the request used '{head.First}'.");
        }

        if (!string.Equals(_upgradeFeature.Protocol, "websocket", StringComparison.OrdinalIgnoreCase))
        {
            throw new WebSocketHandshakeException(400, $"The extended CONNECT :protocol '{_upgradeFeature.Protocol}' is not 'websocket'.");
        }

        ValidateVersion(head, status: 400);
        return CreateRequest(head, key: null);
    }

    private static void ValidateVersion(HttpHead head, int status)
    {
        var version = head[WebSocketHandshake.HeaderVersion];
        if (version.Count != 1 || !string.Equals(version.ToString().Trim(), WebSocketHandshake.SupportedVersion, StringComparison.Ordinal))
        {
            var ex = new WebSocketHandshakeException(status, $"Unsupported Sec-WebSocket-Version '{version}'. Only version 13 is supported.");
            ex.Data[UnsupportedVersionMarker] = true;
            throw ex;
        }
    }

    private WebSocketUpgradeRequest CreateRequest(HttpHead head, string key)
    {
        var origin = head[WebSocketHandshake.HeaderOrigin];
        if (_options.AllowedOrigins.Count > 0 && origin.Count > 0
            && !_options.AllowedOrigins.Any(allowed => string.Equals(allowed, origin.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new WebSocketHandshakeException(403, $"The origin '{origin}' is not allowed.");
        }

        IReadOnlyList<WebSocketExtension> extensions;
        try
        {
            extensions = WebSocketExtension.Parse(head[WebSocketHandshake.HeaderExtensions]);
        }
        catch (FormatException ex)
        {
            throw new WebSocketHandshakeException(400, ex.Message);
        }

        var subProtocols = WebSocketHandshake.ParseTokenList(head[WebSocketHandshake.HeaderProtocol]);
        foreach (var protocol in subProtocols)
        {
            if (!WebSocketHandshake.IsToken(protocol))
            {
                throw new WebSocketHandshakeException(400, $"'{protocol}' is not a valid subprotocol token.");
            }
        }
        return new WebSocketUpgradeRequest(head, key, subProtocols, extensions);
    }

    /// <summary>
    /// Accepts the upgrade (101 Switching Protocols for HTTP/1.1, 200 for HTTP/2) and returns the open session.
    /// </summary>
    /// <param name="subProtocol">The subprotocol to select. It must be one the client offered. When null, the first of
    /// <see cref="WebSocketServerOptions.SubProtocols"/> that the client offered is selected, if any.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async ValueTask<WebSocketSession> AcceptAsync(string subProtocol = null, CancellationToken cancellationToken = default)
    {
        var request = await ReadRequestAsync(cancellationToken).ConfigureAwait(false);
        EnsureNotResponded();

        if (subProtocol != null)
        {
            if (!request.SubProtocols.Contains(subProtocol, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"The subprotocol '{subProtocol}' was not offered by the client.");
            }
        }
        else
        {
            subProtocol = _options.SubProtocols.FirstOrDefault(p => request.SubProtocols.Contains(p, StringComparer.Ordinal));
        }

        PerMessageDeflateParameters compression = null;
        if (_options.Compression != null)
        {
            compression = PerMessageDeflateParameters.NegotiateAsServer(request.Extensions, _options.Compression);
        }

        _responded = true;

        if (_upgradeFeature != null)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!_upgradeFeature.IsExtendedConnect)
            {
                headers[WebSocketHandshake.HeaderAccept] = WebSocketHandshake.ComputeAcceptKey(request.Key);
            }
            if (subProtocol != null)
            {
                headers[WebSocketHandshake.HeaderProtocol] = subProtocol;
            }
            if (compression != null)
            {
                headers[WebSocketHandshake.HeaderExtensions] = compression.ToString();
            }

            var transport = await _upgradeFeature.AcceptAsync(headers, cancellationToken).ConfigureAwait(false);
            return WebSocketSession.Create(transport, _connection, isServer: true, _options, subProtocol, compression);
        }

        var response = new StringBuilder();
        response.Append("HTTP/1.1 101 Switching Protocols\r\n");
        response.Append("Upgrade: websocket\r\n");
        response.Append("Connection: Upgrade\r\n");
        response.Append(WebSocketHandshake.HeaderAccept).Append(": ").Append(WebSocketHandshake.ComputeAcceptKey(request.Key)).Append("\r\n");
        if (subProtocol != null)
        {
            response.Append(WebSocketHandshake.HeaderProtocol).Append(": ").Append(subProtocol).Append("\r\n");
        }
        if (compression != null)
        {
            response.Append(WebSocketHandshake.HeaderExtensions).Append(": ").Append(compression).Append("\r\n");
        }
        response.Append("\r\n");

        await WriteAsync(response.ToString(), cancellationToken).ConfigureAwait(false);

        return WebSocketSession.Create(_connection.Transport, _connection, isServer: true, _options, subProtocol, compression);
    }

    /// <summary>
    /// Rejects the upgrade with an HTTP error response.
    /// </summary>
    public ValueTask RejectAsync(int statusCode, CancellationToken cancellationToken = default)
    {
        if (statusCode is < 400 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Rejections must use a 4xx or 5xx status code.");
        }

        EnsureNotResponded();
        return SendErrorAsync(statusCode, null, cancellationToken);
    }

    /// <summary>
    /// Reads the upgrade request and accepts it in a single call.
    /// </summary>
    public static async ValueTask<WebSocketSession> UpgradeAsync(ConnectionContext connection, WebSocketServerOptions options = null, CancellationToken cancellationToken = default)
    {
        var handshake = new WebSocketServerProtocol(connection, options);
        await handshake.ReadRequestAsync(cancellationToken).ConfigureAwait(false);
        return await handshake.AcceptAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private void EnsureNotResponded()
    {
        if (_responded)
        {
            throw new InvalidOperationException("A response to the upgrade request has already been sent.");
        }
    }

    private async ValueTask SendErrorAsync(int statusCode, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        if (_responded)
        {
            return;
        }

        _responded = true;
        try
        {
            if (_upgradeFeature != null)
            {
                await _upgradeFeature.RejectAsync(statusCode, headers ?? new Dictionary<string, string>(), cancellationToken).ConfigureAwait(false);
                return;
            }

            var response = new StringBuilder();
            response.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(ReasonPhrase(statusCode)).Append("\r\n");
            response.Append("Connection: close\r\nContent-Length: 0\r\n");
            if (headers != null)
            {
                foreach (var (name, value) in headers)
                {
                    response.Append(name).Append(": ").Append(value).Append("\r\n");
                }
            }
            response.Append("\r\n");

            await WriteAsync(response.ToString(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The client may already have gone away
        }
    }

    private async ValueTask WriteAsync(string text, CancellationToken cancellationToken)
    {
        var output = _connection.Transport.Output;
        output.Write(Encoding.ASCII.GetBytes(text));
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        426 => "Upgrade Required",
        429 => "Too Many Requests",
        431 => "Request Header Fields Too Large",
        500 => "Internal Server Error",
        503 => "Service Unavailable",
        _ => "Error"
    };
}
