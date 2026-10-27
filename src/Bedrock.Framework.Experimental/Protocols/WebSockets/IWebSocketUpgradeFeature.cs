using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// Implemented by hosts (such as an HTTP server) that have already parsed the HTTP request carrying a WebSocket
/// upgrade, either an HTTP/1.1 Upgrade (RFC 6455) or an HTTP/2 extended CONNECT (RFC 8441).
/// When present on a connection's features, <see cref="WebSocketServerProtocol"/> validates the request from it
/// and answers through it instead of reading and writing HTTP/1.1 on the connection's transport.
/// </summary>
public interface IWebSocketUpgradeFeature
{
    /// <summary>
    /// True for an HTTP/2 extended CONNECT with :protocol = websocket (RFC 8441); false for an HTTP/1.1 upgrade.
    /// </summary>
    bool IsExtendedConnect { get; }

    /// <summary>
    /// The request method (GET for HTTP/1.1, CONNECT for HTTP/2).
    /// </summary>
    string Method { get; }

    /// <summary>
    /// The request path and query string.
    /// </summary>
    string RequestTarget { get; }

    /// <summary>
    /// The HTTP protocol of the request, for example "HTTP/1.1" or "HTTP/2".
    /// </summary>
    string HttpVersion { get; }

    /// <summary>
    /// For extended CONNECT, the value of the :protocol pseudo-header.
    /// </summary>
    string Protocol { get; }

    /// <summary>
    /// The request headers. For HTTP/2 the authority is exposed as Host.
    /// </summary>
    IReadOnlyDictionary<string, StringValues> Headers { get; }

    /// <summary>
    /// Sends the successful response (101 for HTTP/1.1, 200 for HTTP/2) with the given headers and returns the
    /// upgraded byte stream that carries WebSocket frames.
    /// </summary>
    ValueTask<IDuplexPipe> AcceptAsync(IReadOnlyDictionary<string, string> responseHeaders, CancellationToken cancellationToken);

    /// <summary>
    /// Sends an error response.
    /// </summary>
    ValueTask RejectAsync(int statusCode, IReadOnlyDictionary<string, string> responseHeaders, CancellationToken cancellationToken);
}
