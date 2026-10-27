using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework.Experimental.Protocols.WebSockets;
using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Protocols.WebSockets;
using Xunit;

namespace Bedrock.Framework.Protocols.WebSockets.Tests;

/// <summary>
/// Session-level tests: interop with System.Net.WebSockets in both roles, plus raw-frame protocol violation tests.
/// </summary>
public class WebSocketSessionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static TheoryData<bool, int, WebSocketMessageType> EchoCases()
    {
        var data = new TheoryData<bool, int, WebSocketMessageType>();
        foreach (var compress in new[] { false, true })
        {
            foreach (var size in new[] { 0, 1, 125, 126, 65535, 65536, 300_000 })
            {
                data.Add(compress, size, WebSocketMessageType.Binary);
            }
            data.Add(compress, 5000, WebSocketMessageType.Text);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(EchoCases))]
    public async Task ServerSessionEchoesBclClient(bool compress, int size, WebSocketMessageType type)
    {
        var (ours, peerStream) = CreatePair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive(), compression: compress ? new PerMessageDeflateParameters() : null);
        using var client = WebSocket.CreateFromStream(peerStream, BclOptions(isServer: false, compress));

        var echo = EchoAsync(server);

        var payload = CreatePayload(size, type);
        await client.SendAsync(payload, type, endOfMessage: true, default).WaitAsync(Timeout);
        var (receivedType, received) = await ReceiveBclAsync(client);

        Assert.Equal(type, receivedType);
        Assert.Equal(payload, received);

        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", default).WaitAsync(Timeout);
        await echo.WaitAsync(Timeout);

        Assert.Equal(WebSocketState.Closed, client.State);
        Assert.Equal(WebSocketState.Closed, server.State);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, server.CloseStatus);
        Assert.Equal("done", server.CloseStatusDescription);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, client.CloseStatus);
    }

    [Theory]
    [MemberData(nameof(EchoCases))]
    public async Task ClientSessionEchoesBclServer(bool compress, int size, WebSocketMessageType type)
    {
        var (ours, peerStream) = CreatePair();
        await using var client = WebSocketSession.Create(ours, isServer: false, NoKeepAlive(), compression: compress ? new PerMessageDeflateParameters() : null);
        using var server = WebSocket.CreateFromStream(peerStream, BclOptions(isServer: true, compress));

        var echo = EchoBclAsync(server);

        var payload = CreatePayload(size, type);
        await client.SendAsync(payload, type).AsTask().WaitAsync(Timeout);
        var message = await client.ReadMessageAsync().AsTask().WaitAsync(Timeout);

        Assert.Equal(type, message.MessageType);
        Assert.Equal(payload, message.Data.ToArray());

        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye").AsTask().WaitAsync(Timeout);
        await echo.WaitAsync(Timeout);

        Assert.Equal(WebSocketState.Closed, client.State);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, client.CloseStatus);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, server.CloseStatus);
        Assert.Equal("bye", server.CloseStatusDescription);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public async Task FragmentedMessagesRoundTripInBothDirections(bool compress, bool serverContextTakeover, bool clientContextTakeover)
    {
        var (ours, peerStream) = CreatePair();
        var parameters = compress
            ? new PerMessageDeflateParameters { ServerNoContextTakeover = !serverContextTakeover, ClientNoContextTakeover = !clientContextTakeover }
            : null;

        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive(), compression: parameters);
        var bclOptions = BclOptions(isServer: false, compress);
        if (compress)
        {
            bclOptions.DangerousDeflateOptions.ServerContextTakeover = serverContextTakeover;
            bclOptions.DangerousDeflateOptions.ClientContextTakeover = clientContextTakeover;
        }
        using var client = WebSocket.CreateFromStream(peerStream, bclOptions);

        for (var round = 0; round < 3; round++)
        {
            var text = string.Concat(Enumerable.Repeat($"Round {round}: hello, fragmented world! ☃ ", 200));
            var bytes = Encoding.UTF8.GetBytes(text);

            // BCL client sends in uneven fragments, splitting multi-byte characters across frames
            var offsets = new[] { 0, 1, 7, 333, 2000, bytes.Length };
            for (var i = 0; i < offsets.Length - 1; i++)
            {
                await client.SendAsync(bytes.AsMemory(offsets[i], offsets[i + 1] - offsets[i]), WebSocketMessageType.Text, i == offsets.Length - 2, default).AsTask().WaitAsync(Timeout);
            }

            // Our server reads with a tiny buffer to exercise partial delivery
            var received = new MemoryStream();
            var tiny = new byte[7];
            WebSocketReadResult result;
            do
            {
                result = await server.ReadAsync(tiny).AsTask().WaitAsync(Timeout);
                Assert.Equal(WebSocketMessageType.Text, result.MessageType);
                received.Write(tiny, 0, result.Count);
            }
            while (!result.EndOfMessage);

            Assert.Equal(text, Encoding.UTF8.GetString(received.ToArray()));

            // Our server replies in fragments
            await server.SendAsync(bytes.AsMemory(0, 10), WebSocketMessageType.Text, endOfMessage: false).AsTask().WaitAsync(Timeout);
            await server.SendAsync(bytes.AsMemory(10, 5), WebSocketMessageType.Text, endOfMessage: false).AsTask().WaitAsync(Timeout);
            await server.SendAsync(bytes.AsMemory(15), WebSocketMessageType.Text, endOfMessage: true).AsTask().WaitAsync(Timeout);

            var (type, reply) = await ReceiveBclAsync(client);
            Assert.Equal(WebSocketMessageType.Text, type);
            Assert.Equal(text, Encoding.UTF8.GetString(reply));
        }
    }

    [Fact]
    public async Task ServerInitiatedCloseHandshakeCompletesWhileReadLoopRuns()
    {
        var (ours, peerStream) = CreatePair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());
        using var client = WebSocket.CreateFromStream(peerStream, BclOptions(isServer: false, compress: false));

        // A background read loop on our side, as an application would have
        var readLoop = Task.Run(async () =>
        {
            while (true)
            {
                var message = await server.ReadMessageAsync();
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    return message;
                }
            }
        });

        // The BCL client echoes the close when it receives it
        var clientReceive = Task.Run(async () =>
        {
            var result = await client.ReceiveAsync(new byte[16], default);
            Assert.Equal(WebSocketMessageType.Close, result.MessageType);
            await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, default);
            return result;
        });

        await Task.Delay(50);
        await server.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "shutting down").AsTask().WaitAsync(Timeout);

        var clientResult = await clientReceive.WaitAsync(Timeout);
        Assert.Equal(WebSocketCloseStatus.EndpointUnavailable, clientResult.CloseStatus);
        Assert.Equal("shutting down", clientResult.CloseStatusDescription);

        var closeMessage = await readLoop.WaitAsync(Timeout);
        Assert.Equal(WebSocketMessageType.Close, closeMessage.MessageType);
        Assert.Equal(WebSocketState.Closed, server.State);
    }

    [Fact]
    public async Task CloseAsyncTimesOutAndAbortsWhenPeerNeverResponds()
    {
        var (ours, peer) = CreateRawPair();
        var options = NoKeepAlive();
        options.CloseTimeout = TimeSpan.FromMilliseconds(200);
        await using var server = WebSocketSession.Create(ours, isServer: true, options);

        await server.CloseAsync(WebSocketCloseStatus.NormalClosure).AsTask().WaitAsync(Timeout);

        Assert.Equal(WebSocketState.Aborted, server.State);
        var frame = await ReadRawFrameAsync(peer.Input);
        Assert.Equal(WebSocketOpcode.Close, frame.Header.Opcode);
        Assert.Equal(1000, BinaryPrimitives.ReadUInt16BigEndian(frame.Payload));
    }

    [Fact]
    public async Task PingIsAnsweredWithPongCarryingSamePayloadEvenMidMessage()
    {
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Text, fin: false, "Hel"u8.ToArray());
        await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Ping, fin: true, "are you there?"u8.ToArray());
        await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Continuation, fin: true, "lo"u8.ToArray());

        var message = await server.ReadMessageAsync().AsTask().WaitAsync(Timeout);
        Assert.Equal("Hello", message.GetText());

        var pong = await ReadRawFrameAsync(peer.Input);
        Assert.Equal(WebSocketOpcode.Pong, pong.Header.Opcode);
        Assert.False(pong.Header.Masked);
        Assert.Equal("are you there?", Encoding.UTF8.GetString(pong.Payload));
    }

    [Fact]
    public async Task ClientMasksAndServerDoesNotMask()
    {
        var (clientPipe, clientPeer) = CreateRawPair();
        await using var client = WebSocketSession.Create(clientPipe, isServer: false, NoKeepAlive());
        await client.SendTextAsync("masked").AsTask().WaitAsync(Timeout);
        var clientFrame = await ReadRawFrameAsync(clientPeer.Input);
        Assert.True(clientFrame.Header.Masked);
        Assert.Equal("masked", Encoding.UTF8.GetString(clientFrame.Payload));

        var (serverPipe, serverPeer) = CreateRawPair();
        await using var server = WebSocketSession.Create(serverPipe, isServer: true, NoKeepAlive());
        await server.SendTextAsync("plain").AsTask().WaitAsync(Timeout);
        var serverFrame = await ReadRawFrameAsync(serverPeer.Input);
        Assert.False(serverFrame.Header.Masked);
        Assert.Equal("plain", Encoding.UTF8.GetString(serverFrame.Payload));
    }

    public static TheoryData<string, byte[][], WebSocketCloseStatus> ProtocolViolations() => new()
    {
        // name, raw frames (first byte, payload...), expected close status
        { "continuation without message", [Frame(0x80, "x")], WebSocketCloseStatus.ProtocolError },
        { "new message while fragmented", [Frame(0x01, "a"), Frame(0x81, "b")], WebSocketCloseStatus.ProtocolError },
        { "reserved opcode", [Frame(0x83, "")], WebSocketCloseStatus.ProtocolError },
        { "rsv1 without extension", [Frame(0xC1, "a")], WebSocketCloseStatus.ProtocolError },
        { "rsv2", [Frame(0xA1, "a")], WebSocketCloseStatus.ProtocolError },
        { "fragmented ping", [Frame(0x09, "")], WebSocketCloseStatus.ProtocolError },
        { "close with one byte", [Frame(0x88, new byte[] { 0x03 })], WebSocketCloseStatus.ProtocolError },
        { "close with 1005", [Frame(0x88, new byte[] { 0x03, 0xED })], WebSocketCloseStatus.ProtocolError },
        { "close with 2999", [Frame(0x88, new byte[] { 0x0B, 0xB7 })], WebSocketCloseStatus.ProtocolError },
        { "close with invalid utf8 reason", [Frame(0x88, new byte[] { 0x03, 0xE8, 0xFF })], WebSocketCloseStatus.InvalidPayloadData },
        { "invalid utf8 text", [Frame(0x81, new byte[] { 0xCE, 0xBA, 0xE1, 0xBD, 0xB9, 0xCF, 0x83, 0xCE, 0xBC, 0xCE, 0xB5, 0xED, 0xA0, 0x80, 0x65, 0x64, 0x69, 0x74, 0x65, 0x64 })], WebSocketCloseStatus.InvalidPayloadData },
        { "truncated utf8 at end of message", [Frame(0x01, "ok"), Frame(0x80, new byte[] { 0xE2, 0x82 })], WebSocketCloseStatus.InvalidPayloadData },
    };

    [Theory]
    [MemberData(nameof(ProtocolViolations))]
    public async Task ProtocolViolationsFailTheConnection(string name, byte[][] frames, WebSocketCloseStatus expected)
    {
        _ = name;
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        foreach (var frame in frames)
        {
            await WriteRawFrameAsync(peer.Output, (WebSocketOpcode)(frame[0] & 0x0F), (frame[0] & 0x80) != 0, frame[1..], rsvBits: (byte)(frame[0] & 0x70));
        }

        var ex = await Assert.ThrowsAsync<WebSocketException>(() => server.ReadMessageAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal(WebSocketError.Faulted, ex.WebSocketErrorCode);
        Assert.Equal(expected, server.CloseStatus);
        Assert.Equal(WebSocketState.Aborted, server.State);

        var close = await ReadRawFrameAsync(peer.Input);
        Assert.Equal(WebSocketOpcode.Close, close.Header.Opcode);
        Assert.Equal((int)expected, BinaryPrimitives.ReadUInt16BigEndian(close.Payload));
    }

    [Fact]
    public async Task UnmaskedFrameFromClientFailsTheConnection()
    {
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Text, fin: true, "hi"u8.ToArray(), masked: false);

        await Assert.ThrowsAsync<WebSocketException>(() => server.ReadMessageAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal(WebSocketCloseStatus.ProtocolError, server.CloseStatus);
    }

    [Fact]
    public async Task ValidUtf8SplitAcrossFramesIsAccepted()
    {
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        // U+1F600 (4 bytes) and U+20AC (3 bytes), split at every possible boundary
        var bytes = Encoding.UTF8.GetBytes("a😀€b");
        for (var i = 0; i < bytes.Length; i++)
        {
            await WriteRawFrameAsync(peer.Output, i == 0 ? WebSocketOpcode.Text : WebSocketOpcode.Continuation, i == bytes.Length - 1, bytes[i..(i + 1)]);
        }

        var message = await server.ReadMessageAsync().AsTask().WaitAsync(Timeout);
        Assert.Equal("a😀€b", message.GetText());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MessagesLargerThanTheLimitFailWithMessageTooBig(bool compress)
    {
        var (ours, peerStream) = CreatePair();
        var options = NoKeepAlive();
        options.MaxMessageSize = 64 * 1024;
        await using var server = WebSocketSession.Create(ours, isServer: true, options, compression: compress ? new PerMessageDeflateParameters() : null);
        using var client = WebSocket.CreateFromStream(peerStream, BclOptions(isServer: false, compress));

        // Highly compressible: a deflate "bomb" must be caught on the decompressed size
        var payload = new byte[1024 * 1024];
        var send = client.SendAsync(payload, WebSocketMessageType.Binary, true, default);

        var ex = await Assert.ThrowsAsync<WebSocketException>(() => server.ReadMessageAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, server.CloseStatus);

        var result = await client.ReceiveAsync(new byte[128], default).WaitAsync(Timeout);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, result.CloseStatus);
        _ = send;
    }

    [Fact]
    public async Task TransportClosedWithoutCloseFrameIsReportedAsPrematureClose()
    {
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Text, fin: false, "partial"u8.ToArray());
        await peer.Output.CompleteAsync();

        var ex = await Assert.ThrowsAsync<WebSocketException>(() => server.ReadMessageAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal(WebSocketError.ConnectionClosedPrematurely, ex.WebSocketErrorCode);
        Assert.Equal(WebSocketState.Aborted, server.State);
    }

    [Fact]
    public async Task CloseFrameWithoutStatusIsEchoedEmpty()
    {
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Close, fin: true, []);
        var message = await server.ReadMessageAsync().AsTask().WaitAsync(Timeout);

        Assert.Equal(WebSocketMessageType.Close, message.MessageType);
        Assert.Equal(WebSocketCloseStatus.Empty, message.CloseStatus);

        var echo = await ReadRawFrameAsync(peer.Input);
        Assert.Equal(WebSocketOpcode.Close, echo.Header.Opcode);
        Assert.Empty(echo.Payload);

        // Reads after close keep returning the close result rather than throwing
        var again = await server.ReadAsync(new byte[4]).AsTask().WaitAsync(Timeout);
        Assert.Equal(WebSocketMessageType.Close, again.MessageType);
        await Assert.ThrowsAsync<WebSocketException>(() => server.SendTextAsync("too late").AsTask());
    }

    [Fact]
    public async Task KeepAliveSendsPingsAndAbortsWhenPeerIsSilent()
    {
        var (ours, peer) = CreateRawPair();
        var options = new WebSocketSessionOptions
        {
            KeepAliveInterval = TimeSpan.FromMilliseconds(50),
            KeepAliveTimeout = TimeSpan.FromMilliseconds(150),
        };
        await using var client = WebSocketSession.Create(ours, isServer: false, options);

        var read = client.ReadAsync(new byte[16]).AsTask();

        var ping = await ReadRawFrameAsync(peer.Input);
        Assert.Equal(WebSocketOpcode.Ping, ping.Header.Opcode);
        Assert.True(ping.Header.Masked);

        await Assert.ThrowsAsync<WebSocketException>(() => read.WaitAsync(Timeout));
        Assert.Equal(WebSocketState.Aborted, client.State);
    }

    [Fact]
    public async Task KeepAliveDoesNotAbortWhenPeerResponds()
    {
        var (ours, peerStream) = CreatePair();
        var options = new WebSocketSessionOptions
        {
            KeepAliveInterval = TimeSpan.FromMilliseconds(30),
            KeepAliveTimeout = TimeSpan.FromMilliseconds(200),
        };
        await using var client = WebSocketSession.Create(ours, isServer: false, options);
        using var server = WebSocket.CreateFromStream(peerStream, BclOptions(isServer: true, compress: false));

        var serverLoop = EchoBclAsync(server);
        var read = client.ReadAsync(new byte[16]).AsTask();

        await Task.Delay(600);
        Assert.Equal(WebSocketState.Open, client.State);
        Assert.False(read.IsCompleted);

        await client.SendTextAsync("still here");
        var result = await read.WaitAsync(Timeout);
        Assert.Equal(10, result.Count);
    }

    [Fact]
    public async Task ConcurrentReadsAreRejected()
    {
        var (ours, _) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        var first = server.ReadAsync(new byte[1]);
        Assert.Throws<InvalidOperationException>(() => server.ReadAsync(new byte[1]));
        server.Abort();
        await Assert.ThrowsAsync<WebSocketException>(() => first.AsTask().WaitAsync(Timeout));
    }

    [Theory]
    [InlineData(1004)]
    [InlineData(1005)]
    [InlineData(1006)]
    [InlineData(1015)]
    [InlineData(999)]
    [InlineData(5000)]
    public async Task InvalidCloseStatusesCannotBeSent(int code)
    {
        var (ours, _) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        if (code == 1005)
        {
            // Empty means "no status" and is allowed without a description
            await Assert.ThrowsAsync<ArgumentException>(() => server.CloseOutputAsync((WebSocketCloseStatus)code, "reason").AsTask());
            return;
        }

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => server.CloseOutputAsync((WebSocketCloseStatus)code).AsTask());
    }

    [Fact]
    public async Task SmallMessagesBelowThresholdAreSentUncompressed()
    {
        var (ours, peer) = CreateRawPair();
        var options = NoKeepAlive();
        options.Compression = new WebSocketCompressionOptions { MinimumMessageSize = 64 };
        await using var server = WebSocketSession.Create(ours, isServer: true, options, compression: new PerMessageDeflateParameters());

        await server.SendTextAsync("tiny").AsTask().WaitAsync(Timeout);
        await server.SendTextAsync(new string('z', 200)).AsTask().WaitAsync(Timeout);

        var tiny = await ReadRawFrameAsync(peer.Input);
        Assert.False(tiny.Header.Rsv1);
        Assert.Equal("tiny", Encoding.UTF8.GetString(tiny.Payload));

        var large = await ReadRawFrameAsync(peer.Input);
        Assert.True(large.Header.Rsv1);
        Assert.True(large.Payload.Length < 200);
    }

    [Fact]
    public async Task CompressedBlockWithBfinalDoesNotBreakLaterMessages()
    {
        // RFC 7692 7.2.3.4: a sender may end a message with a BFINAL block even with context takeover
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive(), compression: new PerMessageDeflateParameters());

        await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Text, fin: true, DeflateFinal("hello"u8.ToArray()), rsvBits: 0x40);
        await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Text, fin: true, DeflateFinal("world"u8.ToArray()), rsvBits: 0x40);

        Assert.Equal("hello", (await server.ReadMessageAsync().AsTask().WaitAsync(Timeout)).GetText());
        Assert.Equal("world", (await server.ReadMessageAsync().AsTask().WaitAsync(Timeout)).GetText());
    }

    [Fact]
    public async Task DataAfterBfinalCountsAgainstTheMessageSizeLimit()
    {
        var (ours, peer) = CreateRawPair();
        var options = NoKeepAlive();
        options.MaxMessageSize = 64 * 1024;
        await using var server = WebSocketSession.Create(ours, isServer: true, options, compression: new PerMessageDeflateParameters());

        var writes = Task.Run(async () =>
        {
            await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Binary, fin: false, DeflateFinal([1]), rsvBits: 0x40);
            var junk = new byte[32 * 1024];
            for (var i = 0; i < 16; i++)
            {
                await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Continuation, fin: false, junk);
            }
        });

        await Assert.ThrowsAsync<WebSocketException>(async () =>
        {
            while (true)
            {
                await server.ReadAsync(new byte[4096]).AsTask().WaitAsync(Timeout);
            }
        });
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, server.CloseStatus);
        _ = writes;
    }

    [Fact]
    public async Task LargeCompressedMessagesDecompressIncrementallyIntoSmallReads()
    {
        var (ours, peerStream) = CreatePair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive(), compression: new PerMessageDeflateParameters());
        using var client = WebSocket.CreateFromStream(peerStream, BclOptions(isServer: false, compress: true));

        // 4 MiB that compresses to a few KB; read 16 bytes at a time
        var payload = new byte[4 * 1024 * 1024];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i / 4096);
        }

        for (var message = 0; message < 2; message++)
        {
            var send = client.SendAsync(payload, WebSocketMessageType.Binary, true, default);
            var received = new MemoryStream();
            var small = new byte[16 * 1024];
            WebSocketReadResult result;
            do
            {
                result = await server.ReadAsync(small).AsTask().WaitAsync(Timeout);
                received.Write(small, 0, result.Count);
            }
            while (!result.EndOfMessage);

            await send.WaitAsync(Timeout);
            Assert.Equal(payload, received.ToArray());
        }
    }

    [Fact]
    public async Task PeerThatStopsReadingCannotWedgeCloseOrKeepAlive()
    {
        var options = new PipeOptions(pauseWriterThreshold: 1024, resumeWriterThreshold: 512, useSynchronizationContext: false);
        var pair = DuplexPipe.CreateConnectionPair(options, options);
        var sessionOptions = new WebSocketSessionOptions
        {
            KeepAliveInterval = TimeSpan.FromMilliseconds(100),
            KeepAliveTimeout = TimeSpan.FromMilliseconds(200),
            CloseTimeout = TimeSpan.FromMilliseconds(300),
        };
        await using var server = WebSocketSession.Create(pair.Transport, isServer: true, sessionOptions);

        // The peer never reads, so this send blocks on back-pressure while holding the write lock
        var send = server.SendAsync(new byte[1024 * 1024], WebSocketMessageType.Binary).AsTask();

        var close = server.CloseAsync(WebSocketCloseStatus.NormalClosure).AsTask();
        await close.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(WebSocketState.Aborted, server.State);

        await Assert.ThrowsAnyAsync<Exception>(() => send.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(7777)]
    [InlineData(65537)]
    public async Task CompressedMessagesSurviveReadsOfAnySize(int readSize)
    {
        // Partial reads leave decompressed bytes behind in the session's buffer; they must be kept in order
        var (ours, peerStream) = CreatePair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive(), compression: new PerMessageDeflateParameters());
        using var client = WebSocket.CreateFromStream(peerStream, BclOptions(isServer: false, compress: true));

        var payload = new byte[readSize == 1 ? 50_000 : 400_000];
        var random = new Random(readSize);
        for (var i = 0; i < payload.Length; i++)
        {
            // Compressible but not trivially so: runs of random lengths and values
            payload[i] = (byte)(i % 300 < 150 ? random.Next(4) : i / 1000);
        }

        for (var message = 0; message < 2; message++)
        {
            var send = client.SendAsync(payload, WebSocketMessageType.Binary, true, default);
            var received = new MemoryStream();
            var buffer = new byte[readSize];
            WebSocketReadResult result;
            do
            {
                result = await server.ReadAsync(buffer).AsTask().WaitAsync(Timeout);
                received.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            await send.WaitAsync(Timeout);
            Assert.Equal(payload, received.ToArray());
        }
    }

    [Fact]
    public async Task MaskedFrameFromServerFailsTheConnection()
    {
        // RFC 6455 5.1: a client must close the connection if it receives a masked frame
        var (ours, peer) = CreateRawPair();
        await using var client = WebSocketSession.Create(ours, isServer: false, NoKeepAlive());

        await WriteRawFrameAsync(peer.Output, WebSocketOpcode.Text, fin: true, "hi"u8.ToArray(), masked: true);

        await Assert.ThrowsAsync<WebSocketException>(() => client.ReadMessageAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal(WebSocketCloseStatus.ProtocolError, client.CloseStatus);

        var close = await ReadRawFrameAsync(peer.Input);
        Assert.Equal(WebSocketOpcode.Close, close.Header.Opcode);
        Assert.True(close.Header.Masked);
        Assert.Equal(1002, BinaryPrimitives.ReadUInt16BigEndian(close.Payload));
    }

    public static TheoryData<string, byte[][], WebSocketCloseStatus> CompressedProtocolViolations() => new()
    {
        // RFC 7692 6: RSV1 marks the first frame of a compressed message only
        { "rsv1 on a continuation frame", [Frame(0x41, DeflateFinal("ab"u8.ToArray())), Frame(0xC0, DeflateFinal("cd"u8.ToArray()))], WebSocketCloseStatus.ProtocolError },
        { "rsv1 on a control frame", [Frame(0xC9, "")], WebSocketCloseStatus.ProtocolError },
        // RFC 6455 8.1: text must be valid UTF-8 after decompression
        { "invalid utf8 in compressed text", [Frame(0xC1, DeflateFinal([0x61, 0xFF, 0xFE]))], WebSocketCloseStatus.InvalidPayloadData },
        { "truncated utf8 at end of compressed text", [Frame(0xC1, DeflateFinal([0x6F, 0x6B, 0xE2, 0x82]))], WebSocketCloseStatus.InvalidPayloadData },
        // RFC 7692 8: data that cannot be decompressed fails the connection (BTYPE 11 is reserved)
        { "corrupt deflate data", [Frame(0xC2, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })], WebSocketCloseStatus.InvalidPayloadData },
    };

    [Theory]
    [MemberData(nameof(CompressedProtocolViolations))]
    public async Task CompressedProtocolViolationsFailTheConnection(string name, byte[][] frames, WebSocketCloseStatus expected)
    {
        _ = name;
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive(), compression: new PerMessageDeflateParameters());

        foreach (var frame in frames)
        {
            await WriteRawFrameAsync(peer.Output, (WebSocketOpcode)(frame[0] & 0x0F), (frame[0] & 0x80) != 0, frame[1..], rsvBits: (byte)(frame[0] & 0x70));
        }

        var ex = await Assert.ThrowsAsync<WebSocketException>(() => server.ReadMessageAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal(WebSocketError.Faulted, ex.WebSocketErrorCode);
        Assert.Equal(expected, server.CloseStatus);

        var close = await ReadRawFrameAsync(peer.Input);
        Assert.Equal(WebSocketOpcode.Close, close.Header.Opcode);
        Assert.Equal((int)expected, BinaryPrimitives.ReadUInt16BigEndian(close.Payload));
    }

    [Fact]
    public async Task TransportFailureWhileReadingAbortsTheSession()
    {
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        await peer.Output.CompleteAsync(new IOException("Connection reset"));

        var ex = await Assert.ThrowsAsync<WebSocketException>(() => server.ReadMessageAsync().AsTask().WaitAsync(Timeout));
        Assert.Equal(WebSocketError.ConnectionClosedPrematurely, ex.WebSocketErrorCode);
        Assert.IsType<IOException>(ex.InnerException);
        Assert.Equal(WebSocketState.Aborted, server.State);
    }

    [Fact]
    public async Task TransportFailureWhileWritingAbortsTheSession()
    {
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        await peer.Input.CompleteAsync(new IOException("Broken pipe"));

        var ex = await Assert.ThrowsAsync<WebSocketException>(() => server.SendTextAsync("hello").AsTask().WaitAsync(Timeout));
        Assert.Equal(WebSocketError.ConnectionClosedPrematurely, ex.WebSocketErrorCode);
        Assert.Equal(WebSocketState.Aborted, server.State);
        await Assert.ThrowsAnyAsync<Exception>(() => server.SendTextAsync("again").AsTask().WaitAsync(Timeout));
    }

    [Fact]
    public async Task ControlFramePayloadLimitsAreEnforcedWhenSending()
    {
        // RFC 6455 5.5: control frame payloads are at most 125 bytes, so a close reason is at most 123
        var (ours, peer) = CreateRawPair();
        await using var server = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => server.PingAsync(new byte[126]).AsTask());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => server.PongAsync(new byte[126]).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => server.CloseAsync(WebSocketCloseStatus.NormalClosure, new string('a', 124)).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => server.SendAsync(new byte[1], WebSocketMessageType.Close).AsTask());

        // The limits themselves are allowed, and nothing invalid reached the wire
        await server.PongAsync(new byte[125]).AsTask().WaitAsync(Timeout);
        var pong = await ReadRawFrameAsync(peer.Input);
        Assert.Equal(WebSocketOpcode.Pong, pong.Header.Opcode);
        Assert.Equal(125, pong.Payload.Length);
        Assert.Equal(WebSocketState.Open, server.State);
    }

    [Fact]
    public async Task DuplexPipeKeepsProcessingTheSessionAfterTheApplicationStopsReading()
    {
        var (ours, peerStream) = CreatePair();
        var session = WebSocketSession.Create(ours, isServer: true, NoKeepAlive());
        await using var pipe = new WebSocketDuplexPipe(session);
        using var client = WebSocket.CreateFromStream(peerStream, BclOptions(isServer: false, compress: false));

        await client.SendAsync("first"u8.ToArray(), WebSocketMessageType.Binary, true, default).WaitAsync(Timeout);
        var read = await pipe.Input.ReadAsync().AsTask().WaitAsync(Timeout);
        Assert.Equal("first", Encoding.UTF8.GetString(read.Buffer.ToArray()));

        // The application is done reading, but the peer is still sending
        await pipe.Input.CompleteAsync();
        for (var i = 0; i < 20; i++)
        {
            await client.SendAsync(new byte[10_000], WebSocketMessageType.Binary, true, default).WaitAsync(Timeout);
        }

        // Pings and the peer's Close must still be answered
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", default).WaitAsync(Timeout);
        Assert.Equal(WebSocketState.Closed, client.State);
        Assert.Equal(WebSocketState.Closed, session.State);
    }

    private static byte[] DeflateFinal(byte[] data)
    {
        var output = new MemoryStream();
        using (var deflate = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data);
        }
        return output.ToArray();
    }

    #region Helpers

    private static WebSocketSessionOptions NoKeepAlive() => new() { KeepAliveInterval = TimeSpan.Zero };

    private static WebSocketCreationOptions BclOptions(bool isServer, bool compress) => new()
    {
        IsServer = isServer,
        KeepAliveInterval = TimeSpan.Zero,
        DangerousDeflateOptions = compress ? new WebSocketDeflateOptions() : null,
    };

    private static (IDuplexPipe Ours, Stream Peer) CreatePair()
    {
        var options = new PipeOptions(useSynchronizationContext: false);
        var pair = DuplexPipe.CreateConnectionPair(options, options);
        return (pair.Transport, new DuplexPipeStream(pair.Application.Input, pair.Application.Output));
    }

    private static (IDuplexPipe Ours, IDuplexPipe Peer) CreateRawPair()
    {
        var options = new PipeOptions(useSynchronizationContext: false);
        var pair = DuplexPipe.CreateConnectionPair(options, options);
        return (pair.Transport, pair.Application);
    }

    private static byte[] CreatePayload(int size, WebSocketMessageType type)
    {
        if (type == WebSocketMessageType.Text)
        {
            var text = string.Concat(Enumerable.Repeat("héllo wörld ☃ ", size / 10 + 1));
            return Encoding.UTF8.GetBytes(text);
        }

        var random = new Random(size);
        var payload = new byte[size];
        // Half random, half repetitive so compression has something to work with
        random.NextBytes(payload.AsSpan(0, size / 2));
        return payload;
    }

    private static async Task EchoAsync(WebSocketSession session)
    {
        while (true)
        {
            var message = await session.ReadMessageAsync();
            if (message.MessageType == WebSocketMessageType.Close)
            {
                return;
            }
            await session.SendAsync(message.Data, message.MessageType);
        }
    }

    private static async Task EchoBclAsync(WebSocket socket)
    {
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var stream = new MemoryStream();
            ValueWebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(), default);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (socket.State == WebSocketState.CloseReceived)
                    {
                        await socket.CloseOutputAsync(socket.CloseStatus ?? WebSocketCloseStatus.NormalClosure, socket.CloseStatusDescription, default);
                    }
                    return;
                }
                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            await socket.SendAsync(stream.ToArray(), result.MessageType, true, default);
        }
    }

    private static async Task<(WebSocketMessageType, byte[])> ReceiveBclAsync(WebSocket socket)
    {
        var buffer = new byte[64 * 1024];
        var stream = new MemoryStream();
        ValueWebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer.AsMemory(), default).AsTask().WaitAsync(Timeout);
            stream.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return (result.MessageType, stream.ToArray());
    }

    private static byte[] Frame(byte firstByte, string payload) => Frame(firstByte, Encoding.UTF8.GetBytes(payload));

    private static byte[] Frame(byte firstByte, byte[] payload) => [firstByte, .. payload];

    private static async Task WriteRawFrameAsync(PipeWriter output, WebSocketOpcode opcode, bool fin, byte[] payload, bool masked = true, byte rsvBits = 0)
    {
        var header = new WebSocketHeader(fin, opcode, masked, (ulong)payload.Length, masked ? WebSocketHeader.GenerateMaskingKey() : 0,
            rsv1: (rsvBits & 0x40) != 0, rsv2: (rsvBits & 0x20) != 0, rsv3: (rsvBits & 0x10) != 0);

        // Build the header by hand so invalid frames (e.g. fragmented control frames) can be produced
        var buffer = new ArrayBufferWriter<byte>();
        new WebSocketFrameWriter().WriteMessage(new WebSocketWriteFrame(header, new ReadOnlySequence<byte>(payload)), buffer);
        await output.WriteAsync(buffer.WrittenMemory);
    }

    private sealed record RawFrame(WebSocketHeader Header, byte[] Payload);

    private static async Task<RawFrame> ReadRawFrameAsync(PipeReader input)
    {
        while (true)
        {
            var result = await input.ReadAsync().AsTask().WaitAsync(Timeout);
            var buffer = result.Buffer;
            SequencePosition consumed = default, examined = default;

            if (new WebSocketFrameReader().TryParseMessage(buffer, ref consumed, ref examined, out var frame))
            {
                var rest = buffer.Slice(consumed);
                var length = (long)frame.Header.PayloadLength;
                if (rest.Length >= length)
                {
                    var payload = rest.Slice(0, length).ToArray();
                    if (frame.Header.Masked)
                    {
                        new WebSocketPayloadEncoder(frame.Header.MaskingKey).MaskUnmask(payload);
                    }
                    input.AdvanceTo(rest.GetPosition(length));
                    return new RawFrame(frame.Header, payload);
                }
            }

            if (result.IsCompleted)
            {
                throw new InvalidOperationException("No frame was received.");
            }

            input.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    #endregion
}
