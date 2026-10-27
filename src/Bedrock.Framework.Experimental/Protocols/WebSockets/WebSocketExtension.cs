using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// A single extension entry from a Sec-WebSocket-Extensions header (RFC 6455 section 9.1).
/// </summary>
public sealed class WebSocketExtension
{
    public WebSocketExtension(string name, IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        Name = name;
        Parameters = parameters;
    }

    /// <summary>
    /// The extension token, for example "permessage-deflate".
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The extension parameters in the order they appeared. Parameters without a value have a null value.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Parameters { get; }

    public override string ToString()
    {
        var builder = new StringBuilder(Name);
        foreach (var parameter in Parameters)
        {
            builder.Append("; ").Append(parameter.Key);
            if (parameter.Value != null)
            {
                builder.Append('=').Append(parameter.Value);
            }
        }
        return builder.ToString();
    }

    /// <summary>
    /// Parses all Sec-WebSocket-Extensions header values, preserving order.
    /// </summary>
    /// <exception cref="FormatException">The header value is malformed.</exception>
    public static IReadOnlyList<WebSocketExtension> Parse(StringValues values)
    {
        var result = new List<WebSocketExtension>();
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                ParseValue(value, result);
            }
        }
        return result;
    }

    private static void ParseValue(string value, List<WebSocketExtension> result)
    {
        var position = 0;
        while (true)
        {
            SkipWhitespace(value, ref position);
            if (position >= value.Length)
            {
                return;
            }

            // Tolerate empty list elements (RFC 7230 7)
            if (value[position] == ',')
            {
                position++;
                continue;
            }

            var name = ReadToken(value, ref position);
            var parameters = new List<KeyValuePair<string, string>>();

            while (true)
            {
                SkipWhitespace(value, ref position);
                if (position >= value.Length || value[position] == ',')
                {
                    break;
                }

                if (value[position] != ';')
                {
                    throw new FormatException($"Unexpected character '{value[position]}' in Sec-WebSocket-Extensions.");
                }

                position++;
                SkipWhitespace(value, ref position);
                var parameterName = ReadToken(value, ref position);
                SkipWhitespace(value, ref position);

                string parameterValue = null;
                if (position < value.Length && value[position] == '=')
                {
                    position++;
                    SkipWhitespace(value, ref position);
                    parameterValue = position < value.Length && value[position] == '"'
                        ? ReadQuotedString(value, ref position)
                        : ReadToken(value, ref position);
                }

                parameters.Add(new KeyValuePair<string, string>(parameterName, parameterValue));
            }

            result.Add(new WebSocketExtension(name, parameters));
        }
    }

    private static void SkipWhitespace(string value, ref int position)
    {
        while (position < value.Length && (value[position] == ' ' || value[position] == '\t'))
        {
            position++;
        }
    }

    private static string ReadToken(string value, ref int position)
    {
        var start = position;
        while (position < value.Length && WebSocketHandshake.IsToken(value[position].ToString()))
        {
            position++;
        }

        if (position == start)
        {
            throw new FormatException("Expected a token in Sec-WebSocket-Extensions.");
        }

        return value[start..position];
    }

    private static string ReadQuotedString(string value, ref int position)
    {
        // Opening quote
        position++;
        var builder = new StringBuilder();
        while (position < value.Length)
        {
            var c = value[position++];
            if (c == '"')
            {
                // RFC 6455 9.1: a quoted-string value must still be a valid token once unquoted
                var unquoted = builder.ToString();
                if (!WebSocketHandshake.IsToken(unquoted))
                {
                    throw new FormatException("Quoted extension parameter values must be tokens.");
                }
                return unquoted;
            }

            if (c == '\\')
            {
                if (position >= value.Length)
                {
                    break;
                }
                c = value[position++];
            }

            builder.Append(c);
        }

        throw new FormatException("Unterminated quoted-string in Sec-WebSocket-Extensions.");
    }
}
