using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework.Experimental.Protocols.WebSockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace Bedrock.Framework.Protocols.WebSockets.Tests;

public class WebSocketHandshakeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public void AcceptKeyMatchesRfc6455Example()
    {
        // RFC 6455 section 1.3
        Assert.Equal("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", WebSocketHandshake.ComputeAcceptKey("dGhlIHNhbXBsZSBub25jZQ=="));
    }

    [Fact]
    public void GeneratedKeysAreValidAndUnique()
    {
        var keys = Enumerable.Range(0, 50).Select(_ => WebSocketHandshake.GenerateKey()).ToList();
        Assert.All(keys, k => Assert.True(WebSocketHandshake.IsValidKey(k)));
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.False(WebSocketHandshake.IsValidKey("dG9vIHNob3J0"));
        Assert.False(WebSocketHandshake.IsValidKey("not base64!"));
    }

    [Fact]
    public void ExtensionHeadersParseWithQuotedValues()
    {
        var extensions = WebSocketExtension.Parse(new Microsoft.Extensions.Primitives.StringValues(new[]
        {
            "permessage-deflate; client_max_window_bits, permessage-deflate; server_max_window_bits=\"10\"",
            "x-custom",
        }));

        Assert.Equal(3, extensions.Count);
        Assert.Equal("permessage-deflate", extensions[0].Name);
        Assert.Equal(new KeyValuePair<string, string>("client_max_window_bits", null), extensions[0].Parameters.Single());
        Assert.Equal("10", extensions[1].Parameters.Single().Value);
        Assert.Equal("x-custom", extensions[2].Name);
        Assert.Empty(extensions[2].Parameters);
    }

    [Theory]
    [InlineData("permessage-deflate; =x")]
    [InlineData("permessage-deflate; a=\"unterminated")]
    [InlineData("permessage-deflate; a=\"not a token\"")]
    [InlineData("permessage-deflate x")]
    public void MalformedExtensionHeadersThrow(string value)
    {
        Assert.Throws<FormatException>(() => WebSocketExtension.Parse(value));
    }

    [Fact]
    public async Task ValidRequestIsAcceptedAndBytesAfterTheHandshakeArePreserved()
    {
        var (connection, peer) = CreateConnection();
        var key = WebSocketHandshake.GenerateKey();

        // Send the upgrade request and a first masked frame in a single write
        var frame = new ArrayBufferWriter<byte>();
        new WebSocketFrameWriter().WriteMessage(new WebSocketWriteFrame(WebSocketHeader.CreateMasked(true, WebSocketOpcode.Text, 5), new ReadOnlySequence<byte>("early"u8.ToArray())), frame);
        await peer.Output.WriteAsync(Encoding.ASCII.GetBytes(Request(key, extra: "Sec-WebSocket-Protocol: chat, superchat\r\nOrigin: https://example.com\r\n")).Concat(frame.WrittenSpan.ToArray()).ToArray());

        var options = new WebSocketServerOptions { KeepAliveInterval = TimeSpan.Zero };
        options.SubProtocols.Add("superchat");
        options.AllowedOrigins.Add("https://example.com");

        var handshake = new WebSocketServerProtocol(connection, options);
        var request = await handshake.ReadRequestAsync().AsTask().WaitAsync(Timeout);

        Assert.Equal("/chat", request.Path);
        Assert.Equal("?room=1", request.QueryString);
        Assert.Equal("example.com", request.Host);
        Assert.Equal("https://example.com", request.Origin);
        Assert.Equal(new[] { "chat", "superchat" }, request.SubProtocols);

        await using var session = await handshake.AcceptAsync().AsTask().WaitAsync(Timeout);
        Assert.Equal("superchat", session.SubProtocol);

        var response = await ReadResponseAsync(peer.Input);
        Assert.StartsWith("HTTP/1.1 101 Switching Protocols\r\n", response);
        Assert.Contains("Upgrade: websocket\r\n", response);
        Assert.Contains("Connection: Upgrade\r\n", response);
        Assert.Contains("Sec-WebSocket-Accept: " + WebSocketHandshake.ComputeAcceptKey(key) + "\r\n", response);
        Assert.Contains("Sec-WebSocket-Protocol: superchat\r\n", response);
        Assert.DoesNotContain("Sec-WebSocket-Extensions", response);

        var message = await session.ReadMessageAsync().AsTask().WaitAsync(Timeout);
        Assert.Equal("early", message.GetText());
    }

    public static TheoryData<string, int, string> InvalidRequests() => new()
    {
        { "POST /chat HTTP/1.1\r\nHost: a\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n", 405, "Allow: GET" },
        { "GET /chat HTTP/1.0\r\nHost: a\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nHost: a\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nHost: a\r\nUpgrade: websocket\r\nConnection: keep-alive\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nHost: a\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nHost: a\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: c2hvcnQ=\r\nSec-WebSocket-Version: 13\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nHost: a\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 8\r\n\r\n", 426, "Sec-WebSocket-Version: 13" },
        { "GET /chat HTTP/1.1\r\nHost: a\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\nOrigin: https://evil.example\r\n\r\n", 403, null },
        { "GET /chat HTTP/1.1\r\nHost: a\r\n folded\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nHost: a\r\nX-Evil: a\nInjected: b\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nHost: a\r\nX-Nul: a\0b\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nHost: a\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Protocol: chat, bad/token\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nHost: a\r\nX-Big: " + new string('a', 20000) + "\r\n\r\n", 431, null },
        // Headers that never end must be rejected once they pass the limit, not buffered without bound
        { "GET /chat HTTP/1.1\r\nHost: a\r\nX-Big: " + new string('a', 20000), 431, null },
        { "GET\r\nHost: a\r\n\r\n", 400, null },
        { "GET /chat\r\nHost: a\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nHost a\r\n\r\n", 400, null },
        { "GET /chat HTTP/1.1\r\nBad Name: a\r\n\r\n", 400, null },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequestsAreRejectedWithTheRightStatus(string request, int status, string expectedHeader)
    {
        var (connection, peer) = CreateConnection();
        await peer.Output.WriteAsync(Encoding.ASCII.GetBytes(request));

        var options = new WebSocketServerOptions();
        options.AllowedOrigins.Add("https://good.example");
        var handshake = new WebSocketServerProtocol(connection, options);

        var ex = await Assert.ThrowsAsync<WebSocketHandshakeException>(() => handshake.ReadRequestAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal(status, ex.StatusCode);

        var response = await ReadResponseAsync(peer.Input);
        Assert.StartsWith($"HTTP/1.1 {status} ", response);
        if (expectedHeader != null)
        {
            Assert.Contains(expectedHeader + "\r\n", response);
        }
    }

    [Fact]
    public async Task RejectAsyncSendsTheStatus()
    {
        var (connection, peer) = CreateConnection();
        await peer.Output.WriteAsync(Encoding.ASCII.GetBytes(Request(WebSocketHandshake.GenerateKey())));

        var handshake = new WebSocketServerProtocol(connection);
        await handshake.ReadRequestAsync();
        await handshake.RejectAsync(404);

        Assert.StartsWith("HTTP/1.1 404 Not Found\r\n", await ReadResponseAsync(peer.Input));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handshake.AcceptAsync().AsTask());
    }

    [Theory]
    // Plain offer
    [InlineData("permessage-deflate", true, true, "permessage-deflate")]
    // Client asks the server not to keep context
    [InlineData("permessage-deflate; server_no_context_takeover", true, true, "permessage-deflate; server_no_context_takeover")]
    // Server configured not to keep context and to ask the same of the client
    [InlineData("permessage-deflate; client_max_window_bits", false, false, "permessage-deflate; server_no_context_takeover; client_no_context_takeover")]
    // An offer we cannot honour (server window < 15) is skipped in favour of the next one
    [InlineData("permessage-deflate; server_max_window_bits=10, permessage-deflate", true, true, "permessage-deflate")]
    [InlineData("permessage-deflate; server_max_window_bits=15", true, true, "permessage-deflate; server_max_window_bits=15")]
    // Invalid offers are declined
    [InlineData("permessage-deflate; server_max_window_bits=16", true, true, null)]
    [InlineData("permessage-deflate; server_max_window_bits", true, true, null)]
    [InlineData("permessage-deflate; client_no_context_takeover=1", true, true, null)]
    [InlineData("permessage-deflate; server_no_context_takeover; server_no_context_takeover", true, true, null)]
    [InlineData("permessage-deflate; unknown_param", true, true, null)]
    [InlineData("x-webkit-deflate-frame", true, true, null)]
    public async Task ServerNegotiatesPerMessageDeflate(string offer, bool serverContextTakeover, bool clientContextTakeover, string expected)
    {
        var (connection, peer) = CreateConnection();
        await peer.Output.WriteAsync(Encoding.ASCII.GetBytes(Request(WebSocketHandshake.GenerateKey(), extra: $"Sec-WebSocket-Extensions: {offer}\r\n")));

        var options = new WebSocketServerOptions
        {
            KeepAliveInterval = TimeSpan.Zero,
            Compression = new WebSocketCompressionOptions { ServerContextTakeover = serverContextTakeover, ClientContextTakeover = clientContextTakeover },
        };

        await using var session = await WebSocketServerProtocol.UpgradeAsync(connection, options).AsTask().WaitAsync(Timeout);
        var response = await ReadResponseAsync(peer.Input);

        if (expected == null)
        {
            Assert.Null(session.Compression);
            Assert.DoesNotContain("Sec-WebSocket-Extensions", response);
        }
        else
        {
            Assert.NotNull(session.Compression);
            Assert.Contains($"Sec-WebSocket-Extensions: {expected}\r\n", response);
        }
    }

    [Fact]
    public async Task AcceptingAnUnofferedSubProtocolThrows()
    {
        var (connection, peer) = CreateConnection();
        await peer.Output.WriteAsync(Encoding.ASCII.GetBytes(Request(WebSocketHandshake.GenerateKey(), extra: "Sec-WebSocket-Protocol: chat\r\n")));

        var handshake = new WebSocketServerProtocol(connection);
        await handshake.ReadRequestAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => handshake.AcceptAsync("other").AsTask());
    }

    public static TheoryData<string, string> InvalidResponses() => new()
    {
        { "HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n", "403" },
        { "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: wrong=\r\n\r\n", "Accept" },
        { "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n", "Upgrade: websocket" },
        { "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nSec-WebSocket-Accept: {accept}\r\n\r\n", "Connection: Upgrade" },
        { "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\nSec-WebSocket-Protocol: other\r\n\r\n", "subprotocol" },
        { "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\nSec-WebSocket-Extensions: x-unknown\r\n\r\n", "x-unknown" },
        { "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\nSec-WebSocket-Extensions: permessage-deflate; client_max_window_bits=10\r\n\r\n", "client_max_window_bits" },
        { "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\nSec-WebSocket-Extensions: permessage-deflate, permessage-deflate\r\n\r\n", "not offered" },
        // RFC 6455 4.1: the server may select at most one of the offered subprotocols
        { "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\nSec-WebSocket-Protocol: chat, chat\r\n\r\n", "more than one subprotocol" },
        { "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\nSec-WebSocket-Extensions: permessage-deflate; =x\r\n\r\n", "Expected a token" },
        { "garbage\r\n\r\n", "start line" },
        { "HTTP/1.1 abc OK\r\n\r\n", "malformed" },
    };

    [Theory]
    [MemberData(nameof(InvalidResponses))]
    public async Task ClientRejectsInvalidServerResponses(string response, string expectedMessageFragment)
    {
        var (connection, peer) = CreateConnection();
        var options = new WebSocketClientOptions { KeepAliveInterval = TimeSpan.Zero, Compression = new WebSocketCompressionOptions() };
        options.SubProtocols.Add("chat");

        var connect = WebSocketClientProtocol.ConnectAsync(connection, new Uri("ws://example.com/chat"), options).AsTask();

        var request = await ReadResponseAsync(peer.Input);
        var key = request.Split("\r\n").Single(l => l.StartsWith("Sec-WebSocket-Key: ", StringComparison.Ordinal))["Sec-WebSocket-Key: ".Length..];
        await peer.Output.WriteAsync(Encoding.ASCII.GetBytes(response.Replace("{accept}", WebSocketHandshake.ComputeAcceptKey(key))));

        var ex = await Assert.ThrowsAsync<WebSocketHandshakeException>(() => connect.WaitAsync(Timeout));
        Assert.Contains(expectedMessageFragment, ex.Message);
    }

    [Fact]
    public async Task ConnectionClosedPartWayThroughTheRequestFailsTheHandshake()
    {
        var (connection, peer) = CreateConnection();
        await peer.Output.WriteAsync(Encoding.ASCII.GetBytes("GET /chat HTTP/1.1\r\nHost: exa"));
        await peer.Output.CompleteAsync();

        var ex = await Assert.ThrowsAsync<WebSocketHandshakeException>(() => new WebSocketServerProtocol(connection).ReadRequestAsync().AsTask().WaitAsync(Timeout));
        Assert.Null(ex.StatusCode);
        Assert.Contains("closed before the handshake completed", ex.Message);
    }

    [Fact]
    public async Task ConnectionClosedBeforeAnyRequestFailsTheHandshake()
    {
        var (connection, peer) = CreateConnection();
        await peer.Output.CompleteAsync();

        var ex = await Assert.ThrowsAsync<WebSocketHandshakeException>(() => new WebSocketServerProtocol(connection).ReadRequestAsync().AsTask().WaitAsync(Timeout));
        Assert.Null(ex.StatusCode);
    }

    [Fact]
    public async Task ServerClosingDuringTheResponseFailsTheClientHandshake()
    {
        var (connection, peer) = CreateConnection();
        var connect = WebSocketClientProtocol.ConnectAsync(connection, new Uri("ws://example.com/chat")).AsTask();

        await ReadResponseAsync(peer.Input);
        await peer.Output.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpg"));
        await peer.Output.CompleteAsync();

        await Assert.ThrowsAsync<WebSocketHandshakeException>(() => connect.WaitAsync(Timeout));
    }

    [Theory]
    // RFC 8441 4: the extended CONNECT method with :protocol = websocket
    [InlineData("GET", "websocket", "13", 405, "Allow", "CONNECT")]
    [InlineData("CONNECT", "chat", "13", 400, null, null)]
    // RFC 8441 5: Sec-WebSocket-Version is still required; the error advertises the supported version
    [InlineData("CONNECT", "websocket", "8", 400, "Sec-WebSocket-Version", "13")]
    public async Task InvalidExtendedConnectRequestsAreRejected(string method, string protocol, string version, int status, string header, string headerValue)
    {
        var (connection, _) = CreateConnection();
        var feature = new FakeUpgradeFeature(method, protocol, version);
        connection.Features.Set<IWebSocketUpgradeFeature>(feature);

        var ex = await Assert.ThrowsAsync<WebSocketHandshakeException>(() => new WebSocketServerProtocol(connection).ReadRequestAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(status, feature.RejectedWith);
        if (header != null)
        {
            Assert.Equal(headerValue, feature.RejectionHeaders[header]);
        }
    }

    [Fact]
    public async Task ValidExtendedConnectRequestIsAcceptedThroughTheFeature()
    {
        var (connection, _) = CreateConnection();
        var feature = new FakeUpgradeFeature("CONNECT", "websocket", "13", extensions: "permessage-deflate");
        connection.Features.Set<IWebSocketUpgradeFeature>(feature);

        var options = new WebSocketServerOptions { KeepAliveInterval = TimeSpan.Zero, Compression = new WebSocketCompressionOptions() };
        await using var session = await WebSocketServerProtocol.UpgradeAsync(connection, options).AsTask().WaitAsync(Timeout);

        Assert.NotNull(session.Compression);
        Assert.Equal("permessage-deflate", feature.AcceptedHeaders["Sec-WebSocket-Extensions"]);
        Assert.False(feature.AcceptedHeaders.ContainsKey("Sec-WebSocket-Accept"));
    }

    [Theory]
    [InlineData("ws://example.com/chat?x=1", "GET /chat?x=1 HTTP/1.1", "Host: example.com")]
    [InlineData("ws://example.com:8080", "GET / HTTP/1.1", "Host: example.com:8080")]
    [InlineData("wss://example.com:443/", "GET / HTTP/1.1", "Host: example.com")]
    [InlineData("ws://[::1]:9000/a", "GET /a HTTP/1.1", "Host: [::1]:9000")]
    public async Task ClientRequestHasTheRightTargetAndHost(string uri, string requestLine, string hostHeader)
    {
        var (connection, peer) = CreateConnection();
        var options = new WebSocketClientOptions { Origin = "https://app.example" };
        options.Headers["Authorization"] = "Bearer token";

        _ = WebSocketClientProtocol.ConnectAsync(connection, new Uri(uri), options);

        var request = await ReadResponseAsync(peer.Input);
        var lines = request.Split("\r\n");
        Assert.Equal(requestLine, lines[0]);
        Assert.Contains(hostHeader, lines);
        Assert.Contains("Upgrade: websocket", lines);
        Assert.Contains("Connection: Upgrade", lines);
        Assert.Contains("Sec-WebSocket-Version: 13", lines);
        Assert.Contains("Origin: https://app.example", lines);
        Assert.Contains("Authorization: Bearer token", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("Sec-WebSocket-Extensions", StringComparison.Ordinal));
    }

    #region Helpers

    private static string Request(string key, string extra = "") =>
        "GET /chat?room=1 HTTP/1.1\r\n" +
        "Host: example.com\r\n" +
        "Upgrade: websocket\r\n" +
        "Connection: keep-alive, Upgrade\r\n" +
        $"Sec-WebSocket-Key: {key}\r\n" +
        "Sec-WebSocket-Version: 13\r\n" +
        extra +
        "\r\n";

    private static async Task<string> ReadResponseAsync(PipeReader input)
    {
        while (true)
        {
            var result = await input.ReadAsync().AsTask().WaitAsync(Timeout);
            var text = Encoding.ASCII.GetString(result.Buffer.ToArray());
            var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (end >= 0)
            {
                input.AdvanceTo(result.Buffer.GetPosition(end + 4));
                return text[..(end + 4)];
            }

            if (result.IsCompleted)
            {
                throw new InvalidOperationException("No HTTP head was received: " + text);
            }

            input.AdvanceTo(result.Buffer.Start, result.Buffer.End);
        }
    }

    private static (ConnectionContext Connection, IDuplexPipe Peer) CreateConnection()
    {
        var options = new PipeOptions(useSynchronizationContext: false);
        var pair = DuplexPipe.CreateConnectionPair(options, options);
        return (new TestConnectionContext(pair.Transport), pair.Application);
    }

    private sealed class FakeUpgradeFeature : IWebSocketUpgradeFeature
    {
        public FakeUpgradeFeature(string method, string protocol, string version, string extensions = null)
        {
            Method = method;
            Protocol = protocol;
            var headers = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = "example.com",
                ["Sec-WebSocket-Version"] = version,
            };
            if (extensions != null)
            {
                headers["Sec-WebSocket-Extensions"] = extensions;
            }
            Headers = headers;
        }

        public bool IsExtendedConnect => true;
        public string Method { get; }
        public string RequestTarget => "/chat";
        public string HttpVersion => "HTTP/2";
        public string Protocol { get; }
        public IReadOnlyDictionary<string, Microsoft.Extensions.Primitives.StringValues> Headers { get; }

        public int? RejectedWith { get; private set; }
        public IReadOnlyDictionary<string, string> RejectionHeaders { get; private set; }
        public IReadOnlyDictionary<string, string> AcceptedHeaders { get; private set; }

        public ValueTask<IDuplexPipe> AcceptAsync(IReadOnlyDictionary<string, string> responseHeaders, CancellationToken cancellationToken)
        {
            AcceptedHeaders = responseHeaders;
            var options = new PipeOptions(useSynchronizationContext: false);
            return new ValueTask<IDuplexPipe>(DuplexPipe.CreateConnectionPair(options, options).Transport);
        }

        public ValueTask RejectAsync(int statusCode, IReadOnlyDictionary<string, string> responseHeaders, CancellationToken cancellationToken)
        {
            RejectedWith = statusCode;
            RejectionHeaders = responseHeaders;
            return default;
        }
    }

    private sealed class TestConnectionContext : ConnectionContext
    {
        public TestConnectionContext(IDuplexPipe transport) => Transport = transport;

        public override string ConnectionId { get; set; } = Guid.NewGuid().ToString();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override IDuplexPipe Transport { get; set; }
        public override void Abort(ConnectionAbortedException abortReason) { }
    }

    #endregion
}
