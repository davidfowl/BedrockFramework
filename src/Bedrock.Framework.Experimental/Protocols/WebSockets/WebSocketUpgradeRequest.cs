using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// A validated WebSocket upgrade request received by a server (RFC 6455 section 4.2.1).
/// </summary>
public sealed class WebSocketUpgradeRequest
{
    internal WebSocketUpgradeRequest(HttpHead head, string key, IReadOnlyList<string> subProtocols, IReadOnlyList<WebSocketExtension> extensions)
    {
        Method = head.First;
        RequestTarget = head.Second;
        HttpVersion = head.Third;
        Headers = head.Headers;
        Key = key;
        SubProtocols = subProtocols;
        Extensions = extensions;

        var query = RequestTarget.IndexOf('?');
        Path = query < 0 ? RequestTarget : RequestTarget[..query];
        QueryString = query < 0 ? string.Empty : RequestTarget[query..];
    }

    /// <summary>
    /// The request method (always GET for a valid upgrade).
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// The raw request target, including any query string.
    /// </summary>
    public string RequestTarget { get; }

    /// <summary>
    /// The path portion of the request target.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The query string portion of the request target, including the leading '?', or empty.
    /// </summary>
    public string QueryString { get; }

    /// <summary>
    /// The HTTP version from the request line.
    /// </summary>
    public string HttpVersion { get; }

    /// <summary>
    /// All request headers, keyed case-insensitively.
    /// </summary>
    public IReadOnlyDictionary<string, StringValues> Headers { get; }

    /// <summary>
    /// The Host header.
    /// </summary>
    public string Host => Headers.TryGetValue(WebSocketHandshake.HeaderHost, out var value) ? value.ToString() : null;

    /// <summary>
    /// The Origin header, if the client sent one. Browsers always do.
    /// </summary>
    public string Origin => Headers.TryGetValue(WebSocketHandshake.HeaderOrigin, out var value) ? value.ToString() : null;

    /// <summary>
    /// The Sec-WebSocket-Key nonce.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// The subprotocols the client offered, in its order of preference.
    /// </summary>
    public IReadOnlyList<string> SubProtocols { get; }

    /// <summary>
    /// The extensions the client offered, in its order of preference.
    /// </summary>
    public IReadOnlyList<WebSocketExtension> Extensions { get; }
}
