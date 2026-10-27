using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// Helpers shared by the client and server sides of the RFC 6455 opening handshake.
/// </summary>
public static class WebSocketHandshake
{
    /// <summary>
    /// The only WebSocket protocol version defined by RFC 6455.
    /// </summary>
    public const string SupportedVersion = "13";

    /// <summary>
    /// The GUID appended to the client key when computing Sec-WebSocket-Accept (RFC 6455 section 1.3).
    /// </summary>
    public const string KeyGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    internal const string HeaderHost = "Host";
    internal const string HeaderUpgrade = "Upgrade";
    internal const string HeaderConnection = "Connection";
    internal const string HeaderOrigin = "Origin";
    internal const string HeaderKey = "Sec-WebSocket-Key";
    internal const string HeaderAccept = "Sec-WebSocket-Accept";
    internal const string HeaderVersion = "Sec-WebSocket-Version";
    internal const string HeaderProtocol = "Sec-WebSocket-Protocol";
    internal const string HeaderExtensions = "Sec-WebSocket-Extensions";

    /// <summary>
    /// Generates a new random, base64 encoded, 16 byte Sec-WebSocket-Key value.
    /// </summary>
    public static string GenerateKey()
    {
        Span<byte> key = stackalloc byte[16];
        RandomNumberGenerator.Fill(key);
        return Convert.ToBase64String(key);
    }

    /// <summary>
    /// Computes the Sec-WebSocket-Accept value for the given Sec-WebSocket-Key.
    /// </summary>
    public static string ComputeAcceptKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(Encoding.ASCII.GetBytes(key.Trim() + KeyGuid), hash);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Whether the value is a valid Sec-WebSocket-Key: a base64 encoded 16 byte nonce.
    /// </summary>
    public static bool IsValidKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        Span<byte> decoded = stackalloc byte[18];
        return Convert.TryFromBase64String(key.Trim(), decoded, out var written) && written == 16;
    }

    /// <summary>
    /// Splits a comma separated header (such as Connection or Sec-WebSocket-Protocol) into trimmed, non-empty tokens.
    /// </summary>
    internal static List<string> ParseTokenList(StringValues values)
    {
        var tokens = new List<string>();
        foreach (var value in values)
        {
            if (value == null)
            {
                continue;
            }

            foreach (var token in value.Split(','))
            {
                var trimmed = token.Trim(' ', '\t');
                if (trimmed.Length > 0)
                {
                    tokens.Add(trimmed);
                }
            }
        }
        return tokens;
    }

    internal static bool ContainsToken(StringValues values, string token)
    {
        foreach (var t in ParseTokenList(values))
        {
            if (string.Equals(t, token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether the value is an RFC 7230 token, as required for subprotocol and extension names.
    /// </summary>
    internal static bool IsToken(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c <= 0x20 || c >= 0x7F || "()<>@,;:\\\"/[]?={}".IndexOf(c) >= 0)
            {
                return false;
            }
        }
        return true;
    }

    internal static int GetDefaultPort(Uri uri) =>
        string.Equals(uri.Scheme, "wss", StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;

    internal static string GetHostHeader(Uri uri)
    {
        var host = uri.HostNameType == UriHostNameType.IPv6 ? "[" + uri.IdnHost.Trim('[', ']') + "]" : uri.IdnHost;
        return uri.Port == -1 || uri.Port == GetDefaultPort(uri) ? host : host + ":" + uri.Port;
    }
}

/// <summary>
/// The start line and headers of an HTTP/1.1 request or response.
/// </summary>
internal sealed class HttpHead
{
    public string First { get; init; }
    public string Second { get; init; }
    public string Third { get; init; }
    public Dictionary<string, StringValues> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

    public StringValues this[string name] => Headers.TryGetValue(name, out var value) ? value : StringValues.Empty;

    /// <summary>
    /// Reads an HTTP/1.1 message head directly from a PipeReader, leaving any bytes that follow it unconsumed.
    /// </summary>
    /// <returns>The parsed head, or null if the input completed before any bytes were received.</returns>
    public static async ValueTask<HttpHead> ReadAsync(PipeReader input, bool isRequest, int maxSize, CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await input.ReadAsync(cancellationToken).ConfigureAwait(false);
            var buffer = result.Buffer;

            var consumed = buffer.Start;
            var examined = buffer.End;
            try
            {
                if (TryFindEndOfHead(buffer, out var headEnd))
                {
                    var head = buffer.Slice(0, headEnd);
                    if (head.Length > maxSize)
                    {
                        throw new WebSocketHandshakeException(isRequest ? 431 : null, "The handshake headers exceeded the maximum allowed size.");
                    }

                    consumed = headEnd;
                    examined = headEnd;
                    return Parse(head, isRequest);
                }

                if (buffer.Length > maxSize)
                {
                    throw new WebSocketHandshakeException(isRequest ? 431 : null, "The handshake headers exceeded the maximum allowed size.");
                }

                if (result.IsCanceled)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                if (result.IsCompleted)
                {
                    if (buffer.IsEmpty)
                    {
                        return null;
                    }

                    throw new WebSocketHandshakeException(null, "The connection was closed before the handshake completed.");
                }
            }
            finally
            {
                input.AdvanceTo(consumed, examined);
            }
        }
    }

    private static bool TryFindEndOfHead(in ReadOnlySequence<byte> buffer, out SequencePosition end)
    {
        var reader = new SequenceReader<byte>(buffer);
        if (reader.TryReadTo(out ReadOnlySequence<byte> _, "\r\n\r\n"u8, advancePastDelimiter: true))
        {
            end = reader.Position;
            return true;
        }

        end = default;
        return false;
    }

    private static HttpHead Parse(in ReadOnlySequence<byte> head, bool isRequest)
    {
        // Header values are ISO-8859-1; Latin1 maps every byte so nothing is lost or throws.
        var text = Encoding.Latin1.GetString(head);
        var lines = text.Split("\r\n");

        var startLine = lines[0];
        var firstSpace = startLine.IndexOf(' ');
        var secondSpace = firstSpace < 0 ? -1 : startLine.IndexOf(' ', firstSpace + 1);
        if (firstSpace <= 0 || (isRequest && secondSpace < 0) || ContainsControlCharacters(startLine))
        {
            throw Malformed(isRequest, "Malformed HTTP start line.");
        }

        HttpHead result;
        if (secondSpace < 0)
        {
            // A response may omit the reason phrase
            result = new HttpHead { First = startLine[..firstSpace], Second = startLine[(firstSpace + 1)..], Third = string.Empty };
        }
        else
        {
            result = new HttpHead { First = startLine[..firstSpace], Second = startLine[(firstSpace + 1)..secondSpace], Third = startLine[(secondSpace + 1)..] };
        }

        // The head ends with an empty line; skip it
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            if (line[0] == ' ' || line[0] == '\t')
            {
                // RFC 7230 3.2.4: obsolete line folding must be rejected
                throw Malformed(isRequest, "Obsolete header line folding is not supported.");
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                throw Malformed(isRequest, "Malformed HTTP header.");
            }

            var name = line[..colon];
            if (!WebSocketHandshake.IsToken(name))
            {
                throw Malformed(isRequest, $"Invalid HTTP header name '{name}'.");
            }

            var value = line[(colon + 1)..].Trim(' ', '\t');
            if (ContainsControlCharacters(value))
            {
                // Bare CR, LF, NUL and other controls must never reach a response (RFC 7230 3.2)
                throw Malformed(isRequest, $"The value of header '{name}' contains control characters.");
            }
            result.Headers[name] = result.Headers.TryGetValue(name, out var existing) ? StringValues.Concat(existing, value) : new StringValues(value);
        }

        return result;
    }

    private static WebSocketHandshakeException Malformed(bool isRequest, string message) => new(isRequest ? 400 : null, message);

    internal static bool ContainsControlCharacters(string value)
    {
        foreach (var c in value)
        {
            if ((c < 0x20 && c != '\t') || c == 0x7F)
            {
                return true;
            }
        }
        return false;
    }
}
