using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Bedrock.Framework.Protocols.WebSockets;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// The negotiated parameters of the permessage-deflate extension (RFC 7692 section 7).
/// </summary>
public sealed class PerMessageDeflateParameters
{
    public const string ExtensionName = "permessage-deflate";

    /// <summary>
    /// The server resets its compression context after each message.
    /// </summary>
    public bool ServerNoContextTakeover { get; init; }

    /// <summary>
    /// The client resets its compression context after each message.
    /// </summary>
    public bool ClientNoContextTakeover { get; init; }

    /// <summary>
    /// The LZ77 window size, in bits, the server compresses with (8-15).
    /// </summary>
    public int ServerMaxWindowBits { get; init; } = 15;

    /// <summary>
    /// The LZ77 window size, in bits, the client compresses with (8-15).
    /// </summary>
    public int ClientMaxWindowBits { get; init; } = 15;

    /// <summary>
    /// Whether <see cref="ServerMaxWindowBits"/> was explicitly included in the negotiation.
    /// </summary>
    internal bool ServerMaxWindowBitsSpecified { get; init; }

    /// <summary>
    /// Whether <see cref="ClientMaxWindowBits"/> was explicitly included in the negotiation.
    /// </summary>
    internal bool ClientMaxWindowBitsSpecified { get; init; }

    /// <summary>
    /// Formats the parameters as a Sec-WebSocket-Extensions value.
    /// </summary>
    public override string ToString()
    {
        var builder = new StringBuilder(ExtensionName);
        if (ServerNoContextTakeover)
        {
            builder.Append("; server_no_context_takeover");
        }
        if (ClientNoContextTakeover)
        {
            builder.Append("; client_no_context_takeover");
        }
        if (ServerMaxWindowBitsSpecified)
        {
            builder.Append("; server_max_window_bits=").Append(ServerMaxWindowBits);
        }
        if (ClientMaxWindowBitsSpecified)
        {
            builder.Append("; client_max_window_bits=").Append(ClientMaxWindowBits);
        }
        return builder.ToString();
    }

    /// <summary>
    /// The offer a client sends. client_max_window_bits is intentionally not offered: the managed deflate
    /// implementation always compresses with a 15 bit window, so the client cannot honour a smaller one.
    /// </summary>
    internal static string CreateClientOffer(WebSocketCompressionOptions options)
    {
        var builder = new StringBuilder(ExtensionName);
        if (!options.ClientContextTakeover)
        {
            builder.Append("; client_no_context_takeover");
        }
        if (!options.ServerContextTakeover)
        {
            builder.Append("; server_no_context_takeover");
        }
        return builder.ToString();
    }

    /// <summary>
    /// Server side: selects the first acceptable permessage-deflate offer (RFC 7692 section 5 and 7.1).
    /// </summary>
    /// <returns>The accepted parameters, or null if no offer could be accepted.</returns>
    internal static PerMessageDeflateParameters NegotiateAsServer(IReadOnlyList<WebSocketExtension> offers, WebSocketCompressionOptions options)
    {
        foreach (var offer in offers)
        {
            if (!string.Equals(offer.Name, ExtensionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryReadParameters(offer, isOffer: true, out var serverNoContext, out var clientNoContext, out var serverBits, out var clientBits))
            {
                continue;
            }

            // We cannot compress with a window smaller than 15 bits, so decline offers that require it.
            if (serverBits is < 15)
            {
                continue;
            }

            return new PerMessageDeflateParameters
            {
                ServerNoContextTakeover = serverNoContext || !options.ServerContextTakeover,
                ClientNoContextTakeover = clientNoContext || !options.ClientContextTakeover,
                ServerMaxWindowBits = 15,
                ServerMaxWindowBitsSpecified = serverBits.HasValue,
                // The decompressor accepts any window, so we never constrain the client.
                ClientMaxWindowBits = 15,
            };
        }

        return null;
    }

    /// <summary>
    /// Client side: validates the server's response to our offer (RFC 7692 section 7.1).
    /// </summary>
    /// <exception cref="WebSocketHandshakeException">The response is not a valid acceptance of the offer.</exception>
    internal static PerMessageDeflateParameters ValidateServerResponse(WebSocketExtension response, WebSocketCompressionOptions options)
    {
        if (!TryReadParameters(response, isOffer: false, out var serverNoContext, out var clientNoContext, out var serverBits, out var clientBits))
        {
            throw new WebSocketHandshakeException(101, $"The server responded with invalid permessage-deflate parameters: '{response}'.");
        }

        if (clientBits.HasValue)
        {
            // RFC 7692 7.1.2.2: the server must not send client_max_window_bits unless the client offered it.
            throw new WebSocketHandshakeException(101, "The server responded with client_max_window_bits, which was not offered.");
        }

        return new PerMessageDeflateParameters
        {
            ServerNoContextTakeover = serverNoContext,
            ClientNoContextTakeover = clientNoContext || !options.ClientContextTakeover,
            ServerMaxWindowBits = serverBits ?? 15,
            ServerMaxWindowBitsSpecified = serverBits.HasValue,
            ClientMaxWindowBits = 15,
        };
    }

    private static bool TryReadParameters(WebSocketExtension extension, bool isOffer,
        out bool serverNoContext, out bool clientNoContext, out int? serverBits, out int? clientBits)
    {
        serverNoContext = false;
        clientNoContext = false;
        serverBits = null;
        clientBits = null;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in extension.Parameters)
        {
            // RFC 7692 7: each parameter must appear at most once
            if (!seen.Add(name))
            {
                return false;
            }

            switch (name.ToLowerInvariant())
            {
                case "server_no_context_takeover":
                    if (value != null) return false;
                    serverNoContext = true;
                    break;
                case "client_no_context_takeover":
                    if (value != null) return false;
                    clientNoContext = true;
                    break;
                case "server_max_window_bits":
                    if (!TryParseWindowBits(value, out var s)) return false;
                    serverBits = s;
                    break;
                case "client_max_window_bits":
                    if (value == null)
                    {
                        // A value-less client_max_window_bits is only valid in an offer
                        if (!isOffer) return false;
                        break;
                    }
                    if (!TryParseWindowBits(value, out var c)) return false;
                    clientBits = c;
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool TryParseWindowBits(string value, out int bits)
    {
        bits = 0;
        return value != null
            && value.Length is 1 or 2
            && value[0] != '0'
            && int.TryParse(value, System.Globalization.NumberStyles.None, null, out bits)
            && bits is >= 8 and <= 15;
    }
}

/// <summary>
/// Compresses outgoing messages for permessage-deflate.
/// </summary>
internal sealed class PerMessageDeflater : IDisposable
{
    private static readonly byte[] EmptyMessage = [0x00];

    private readonly CompressionLevel _level;
    private readonly bool _noContextTakeover;
    private readonly MemoryStream _output = new();
    private DeflateStream _deflate;

    public PerMessageDeflater(CompressionLevel level, bool noContextTakeover)
    {
        _level = level;
        _noContextTakeover = noContextTakeover;
    }

    /// <summary>
    /// Compresses part of a message. The returned memory is only valid until the next call.
    /// </summary>
    public ReadOnlyMemory<byte> Compress(ReadOnlySpan<byte> data, bool endOfMessage)
    {
        _deflate ??= new DeflateStream(_output, _level, leaveOpen: true);
        _output.SetLength(0);

        if (!data.IsEmpty)
        {
            _deflate.Write(data);
        }

        // A sync flush aligns the output to a byte boundary and ends it with 00 00 FF FF
        _deflate.Flush();

        var written = new ReadOnlyMemory<byte>(_output.GetBuffer(), 0, (int)_output.Length);

        if (endOfMessage)
        {
            if (_noContextTakeover)
            {
                _deflate.Dispose();
                _deflate = null;
            }

            // RFC 7692 7.2.1: remove the trailing 00 00 FF FF of the final flush
            if (EndsWithFlushMarker(written.Span))
            {
                written = written[..^4];
            }

            // RFC 7692 7.2.3.6: an empty message is sent as a single 0x00 byte
            if (written.IsEmpty)
            {
                return EmptyMessage;
            }
        }

        return written;
    }

    private static bool EndsWithFlushMarker(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && data[^4] == 0x00 && data[^3] == 0x00 && data[^2] == 0xFF && data[^1] == 0xFF;

    public void Dispose()
    {
        _deflate?.Dispose();
        _output.Dispose();
    }
}

/// <summary>
/// Decompresses incoming permessage-deflate messages. Compressed bytes are pushed in as they arrive and
/// decompressed output is pulled out, so a single message never has to be fully buffered in compressed form.
/// </summary>
internal sealed class PerMessageInflater : IDisposable
{
    private static readonly byte[] FlushMarker = [0x00, 0x00, 0xFF, 0xFF];

    private readonly bool _noContextTakeover;
    private readonly InputStream _input = new();
    private DeflateStream _inflate;

    public PerMessageInflater(bool noContextTakeover)
    {
        _noContextTakeover = noContextTakeover;
    }

    /// <summary>
    /// Whether the deflate stream ended (a BFINAL=1 block, RFC 7692 7.2.3.4). Any remaining input for the current
    /// message is discarded and a new stream is started for the next message.
    /// </summary>
    public bool IsFinished { get; private set; }

    /// <summary>
    /// The number of pushed bytes the decompressor has not consumed yet.
    /// </summary>
    public int BufferedInput => _input.Available;

    /// <summary>
    /// Adds compressed payload bytes.
    /// </summary>
    public void Push(ReadOnlySpan<byte> compressed)
    {
        if (!IsFinished)
        {
            _input.Append(compressed);
        }
    }

    /// <summary>
    /// Unmasks into the input buffer directly, without touching the source memory.
    /// </summary>
    internal void Push(ReadOnlySpan<byte> masked, ref WebSocketPayloadEncoder encoder)
    {
        if (IsFinished)
        {
            // Keep the masking key position in step even though the bytes are dropped
            Span<byte> scratch = stackalloc byte[256];
            while (!masked.IsEmpty)
            {
                var chunk = masked[..Math.Min(masked.Length, scratch.Length)];
                encoder.MaskUnmask(scratch[..chunk.Length]);
                masked = masked[chunk.Length..];
            }
            return;
        }

        var destination = _input.GetAppendSpan(masked.Length);
        masked.CopyTo(destination);
        encoder.MaskUnmask(destination);
        _input.Advance(masked.Length);
    }

    /// <summary>
    /// Signals the end of the compressed message by appending the removed 00 00 FF FF marker.
    /// </summary>
    public void EndMessage() => Push(FlushMarker);

    /// <summary>
    /// Decompresses into <paramref name="destination"/>. Returns 0 when all pushed input has been consumed.
    /// </summary>
    public int Inflate(Span<byte> destination)
    {
        if (IsFinished || destination.IsEmpty)
        {
            return 0;
        }

        _inflate ??= new DeflateStream(_input, CompressionMode.Decompress, leaveOpen: true);
        try
        {
            var readsBefore = _input.Reads;
            var written = _inflate.Read(destination);

            // A DeflateStream that needs input always asks the underlying stream for more; one that has
            // reached the end of a BFINAL block returns 0 without reading.
            if (written == 0 && _input.Reads == readsBefore)
            {
                IsFinished = true;
                _input.Clear();
            }

            return written;
        }
        catch (InvalidDataException ex)
        {
            throw new WebSocketCompressionException("The compressed message payload is invalid.", ex);
        }
    }

    /// <summary>
    /// Called once a message's output has been fully drained.
    /// </summary>
    public void Reset()
    {
        if ((_noContextTakeover || IsFinished) && _inflate != null)
        {
            _inflate.Dispose();
            _inflate = null;
        }
        IsFinished = false;
        _input.Clear();
    }

    public void Dispose()
    {
        _inflate?.Dispose();
    }

    /// <summary>
    /// A readable stream over pushed bytes that reports "no data" (0) rather than end-of-stream when empty,
    /// which DeflateStream treats as "needs more input" and resumes from on the next Read.
    /// </summary>
    private sealed class InputStream : Stream
    {
        private byte[] _buffer = new byte[4096];
        private int _start;
        private int _end;

        public int Reads { get; private set; }

        public int Available => _end - _start;

        public void Append(ReadOnlySpan<byte> data)
        {
            data.CopyTo(GetAppendSpan(data.Length));
            _end += data.Length;
        }

        public Span<byte> GetAppendSpan(int length)
        {
            if (_start == _end)
            {
                _start = _end = 0;
            }

            if (_buffer.Length - _end < length)
            {
                var unread = _end - _start;
                if (_buffer.Length < unread + length)
                {
                    var larger = new byte[Math.Max(_buffer.Length * 2, unread + length)];
                    _buffer.AsSpan(_start, unread).CopyTo(larger);
                    _buffer = larger;
                }
                else
                {
                    _buffer.AsSpan(_start, unread).CopyTo(_buffer);
                }
                _start = 0;
                _end = unread;
            }

            return _buffer.AsSpan(_end, length);
        }

        public void Advance(int count) => _end += count;

        public void Clear() => _start = _end = 0;

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            Reads++;
            var count = Math.Min(buffer.Length, _end - _start);
            _buffer.AsSpan(_start, count).CopyTo(buffer);
            _start += count;
            return count;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

/// <summary>
/// Thrown when a compressed message cannot be decompressed.
/// </summary>
internal sealed class WebSocketCompressionException : Exception
{
    public WebSocketCompressionException(string message, Exception inner) : base(message, inner) { }
}
