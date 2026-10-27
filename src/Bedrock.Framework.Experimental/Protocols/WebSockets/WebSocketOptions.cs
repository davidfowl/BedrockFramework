using System;
using System.Collections.Generic;
using System.IO.Compression;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// Options that control an open WebSocket session, shared by clients and servers.
/// </summary>
public class WebSocketSessionOptions
{
    /// <summary>
    /// How often to send a keep-alive Ping frame. <see cref="TimeSpan.Zero"/> or <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> disables keep-alive pings.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to wait for any frame from the peer after sending a keep-alive Ping before aborting the session.
    /// <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> (the default) never aborts. Pongs are only observed while the session is being read.
    /// </summary>
    public TimeSpan KeepAliveTimeout { get; set; } = System.Threading.Timeout.InfiniteTimeSpan;

    /// <summary>
    /// The maximum size, in bytes, of a single received message (after decompression). Larger messages fail the
    /// connection with status 1009 (Message Too Big). Null means unlimited. Defaults to 16 MiB.
    /// </summary>
    public long? MaxMessageSize { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// How long <see cref="WebSocketSession.CloseAsync"/> waits for the peer's Close frame before aborting. Defaults to 10 seconds.
    /// </summary>
    public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Enables the permessage-deflate extension (RFC 7692) when non-null. Disabled by default.
    /// </summary>
    public WebSocketCompressionOptions Compression { get; set; }

    internal void Validate()
    {
        if (MaxMessageSize is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxMessageSize));
        }
    }
}

/// <summary>
/// Options for the server side of a WebSocket connection.
/// </summary>
public class WebSocketServerOptions : WebSocketSessionOptions
{
    /// <summary>
    /// The maximum size, in bytes, of the HTTP upgrade request head. Defaults to 16 KiB.
    /// </summary>
    public int MaxHandshakeSize { get; set; } = 16 * 1024;

    /// <summary>
    /// When non-empty, only upgrade requests whose Origin header matches one of these values (case-insensitively) are accepted.
    /// Requests from other origins are rejected with 403. Requests without an Origin header (non-browser clients) are always allowed.
    /// </summary>
    public IList<string> AllowedOrigins { get; } = new List<string>();

    /// <summary>
    /// The subprotocols this server supports, in order of preference. When the client offers any of them, the first
    /// supported one in this list is selected automatically unless one is passed to <see cref="WebSocketServerProtocol.AcceptAsync(string, System.Threading.CancellationToken)"/>.
    /// </summary>
    public IList<string> SubProtocols { get; } = new List<string>();
}

/// <summary>
/// Options for the client side of a WebSocket connection.
/// </summary>
public class WebSocketClientOptions : WebSocketSessionOptions
{
    /// <summary>
    /// The subprotocols to offer in Sec-WebSocket-Protocol, in order of preference.
    /// </summary>
    public IList<string> SubProtocols { get; } = new List<string>();

    /// <summary>
    /// An optional Origin header value to send.
    /// </summary>
    public string Origin { get; set; }

    /// <summary>
    /// Additional headers to send with the upgrade request (for example Authorization or Cookie).
    /// </summary>
    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The maximum size, in bytes, of the HTTP upgrade response head. Defaults to 16 KiB.
    /// </summary>
    public int MaxHandshakeSize { get; set; } = 16 * 1024;
}

/// <summary>
/// Options for the permessage-deflate extension (RFC 7692).
/// </summary>
public class WebSocketCompressionOptions
{
    /// <summary>
    /// Whether the server keeps its compression context between messages. Disabling this (server_no_context_takeover)
    /// lowers memory use at the cost of compression ratio. Defaults to true.
    /// </summary>
    public bool ServerContextTakeover { get; set; } = true;

    /// <summary>
    /// Whether the client keeps its compression context between messages (client_no_context_takeover when false). Defaults to true.
    /// </summary>
    public bool ClientContextTakeover { get; set; } = true;

    /// <summary>
    /// The compression level used for outgoing messages. Defaults to <see cref="CompressionLevel.Fastest"/>.
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Fastest;

    /// <summary>
    /// Whole messages smaller than this many bytes are sent uncompressed (RSV1 clear), which RFC 7692 permits
    /// and avoids expanding tiny payloads. Defaults to 0 (compress everything).
    /// </summary>
    public int MinimumMessageSize { get; set; }
}
