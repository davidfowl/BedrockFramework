using System;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// Thrown when the WebSocket opening handshake fails.
/// </summary>
public class WebSocketHandshakeException : Exception
{
    private static readonly IReadOnlyDictionary<string, StringValues> EmptyHeaders = new Dictionary<string, StringValues>();

    public WebSocketHandshakeException(int? statusCode, string message)
        : this(statusCode, message, null)
    {
    }

    public WebSocketHandshakeException(int? statusCode, string message, IReadOnlyDictionary<string, StringValues> headers)
        : base(message)
    {
        StatusCode = statusCode;
        Headers = headers ?? EmptyHeaders;
    }

    /// <summary>
    /// On the server, the HTTP status code that was (or should be) sent in response to the failed upgrade request.
    /// On the client, the HTTP status code the server responded with, if a response was received.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// On the client, the headers of the server's response, if a response was received.
    /// </summary>
    public IReadOnlyDictionary<string, StringValues> Headers { get; }
}
