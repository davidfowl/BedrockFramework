using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework.Experimental.Protocols.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Bedrock.Framework.Protocols.WebSockets.Tests;

/// <summary>
/// RFC 8441 (WebSockets over HTTP/2 extended CONNECT) tests.
/// </summary>
public class WebSocketHttp2Tests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Http2ClientTalksToHttp2Listener(bool compress, bool tls)
    {
        using var certificate = tls ? CreateCertificate() : null;
        await using var host = await StartEchoServerAsync(listener =>
        {
            if (tls && compress)
            {
                // Configure TLS entirely at the Kestrel layer
                listener.ConfigureHttps = https => https.ServerCertificateSelector = (_, _) => certificate;
            }
            else if (tls)
            {
                listener.Certificate = certificate;
            }
        }, compress, tls);

        var options = new WebSocketClientOptions { KeepAliveInterval = TimeSpan.Zero, Compression = compress ? new WebSocketCompressionOptions() : null };
        options.SubProtocols.Add("echo.v1");

        using var invoker = CreateInvoker();
        await using var session = await WebSocketHttp2Client.ConnectAsync(new Uri($"{(tls ? "wss" : "ws")}://127.0.0.1:{host.Port}/echo?x=1"), options, invoker).AsTask().WaitAsync(Timeout);

        Assert.Equal("echo.v1", session.SubProtocol);
        Assert.Equal(compress, session.Compression != null);
        Assert.Equal("/echo?x=1", await host.LastTarget.Task.WaitAsync(Timeout));
        Assert.Equal("HTTP/2", await host.LastVersion.Task.WaitAsync(Timeout));

        await AssertEchoAsync(session);

        await session.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye").AsTask().WaitAsync(Timeout);
        Assert.Equal(WebSocketState.Closed, session.State);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, session.CloseStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientWebSocketOverHttp2TalksToHttp2Listener(bool compress)
    {
        await using var host = await StartEchoServerAsync(null, compress, tls: false);

        using var client = new ClientWebSocket();
        client.Options.HttpVersion = HttpVersion.Version20;
        client.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        client.Options.AddSubProtocol("echo.v1");
        if (compress)
        {
            client.Options.DangerousDeflateOptions = new WebSocketDeflateOptions();
        }

        using var invoker = CreateInvoker();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{host.Port}/echo"), invoker, default).WaitAsync(Timeout);
        Assert.Equal("echo.v1", client.SubProtocol);
        Assert.Equal("HTTP/2", await host.LastVersion.Task.WaitAsync(Timeout));

        foreach (var size in new[] { 0, 100, 70_000 })
        {
            var payload = Encoding.UTF8.GetBytes(new string('h', size));
            await client.SendAsync(payload, WebSocketMessageType.Text, true, default).WaitAsync(Timeout);

            var buffer = new byte[100_000];
            var received = new MemoryStream();
            ValueWebSocketReceiveResult result;
            do
            {
                result = await client.ReceiveAsync(buffer.AsMemory(), default).AsTask().WaitAsync(Timeout);
                received.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            Assert.Equal(payload, received.ToArray());
        }

        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", default).WaitAsync(Timeout);
        Assert.Equal(WebSocketState.Closed, client.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Http2ClientTalksToKestrelWebSocketsOverHttp2(bool compress)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, o => o.Protocols = HttpProtocols.Http2));
        await using var app = builder.Build();

        string protocol = null;
        app.UseWebSockets();
        app.Map("/echo", async context =>
        {
            protocol = context.Request.Protocol;
            using var socket = await context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext { DangerousEnableCompression = compress });
            var buffer = new byte[256 * 1024];
            while (true)
            {
                var stream = new MemoryStream();
                ValueWebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer.AsMemory(), default);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, default);
                        return;
                    }
                    stream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);
                await socket.SendAsync(stream.ToArray(), result.MessageType, true, default);
            }
        });

        await app.StartAsync();
        var port = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First()).Port;

        using var invoker = CreateInvoker();
        var options = new WebSocketClientOptions { KeepAliveInterval = TimeSpan.Zero, Compression = compress ? new WebSocketCompressionOptions() : null };
        await using var session = await WebSocketHttp2Client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/echo"), options, invoker).AsTask().WaitAsync(Timeout);

        Assert.Equal("HTTP/2", protocol);
        Assert.Equal(compress, session.Compression != null);

        await AssertEchoAsync(session);
        await session.CloseAsync(WebSocketCloseStatus.NormalClosure).AsTask().WaitAsync(Timeout);
        Assert.Equal(WebSocketState.Closed, session.State);

        await app.StopAsync();
    }

    [Fact]
    public async Task ManySessionsMultiplexOverOneHttp2Connection()
    {
        await using var host = await StartEchoServerAsync(null, compress: true, tls: false);

        var handler = new SocketsHttpHandler();
        var connections = 0;
        handler.ConnectCallback = async (context, cancellationToken) =>
        {
            System.Threading.Interlocked.Increment(ref connections);
            var socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp) { NoDelay = true };
            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
            return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
        };
        using var invoker = new HttpMessageInvoker(handler);

        var options = new WebSocketClientOptions { KeepAliveInterval = TimeSpan.Zero, Compression = new WebSocketCompressionOptions() };
        var sessions = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            WebSocketHttp2Client.ConnectAsync(new Uri($"ws://127.0.0.1:{host.Port}/echo"), options, invoker).AsTask())).WaitAsync(Timeout);

        await Task.WhenAll(sessions.Select(async (session, i) =>
        {
            for (var round = 0; round < 10; round++)
            {
                var text = $"session {i} round {round}";
                await session.SendTextAsync(text);
                var message = await session.ReadMessageAsync();
                Assert.Equal(text, message.GetText());
            }
        })).WaitAsync(Timeout);

        Assert.Equal(1, connections);

        foreach (var session in sessions)
        {
            await session.CloseAsync(WebSocketCloseStatus.NormalClosure).AsTask().WaitAsync(Timeout);
            await session.DisposeAsync();
        }
    }

    [Fact]
    public async Task Http1UpgradesAreAlsoAcceptedByTheKestrelListener()
    {
        await using var host = await StartEchoServerAsync(listener => listener.Protocols = HttpProtocols.Http1, compress: true, tls: false);

        using var client = new ClientWebSocket();
        client.Options.AddSubProtocol("echo.v1");
        client.Options.DangerousDeflateOptions = new WebSocketDeflateOptions();
        await client.ConnectAsync(new Uri($"ws://127.0.0.1:{host.Port}/echo"), default).WaitAsync(Timeout);

        Assert.Equal("HTTP/1.1", await host.LastVersion.Task.WaitAsync(Timeout));
        Assert.Equal("echo.v1", client.SubProtocol);

        await client.SendAsync("over http/1.1"u8.ToArray(), WebSocketMessageType.Text, true, default).WaitAsync(Timeout);
        var buffer = new byte[64];
        var result = await client.ReceiveAsync(buffer, default).WaitAsync(Timeout);
        Assert.Equal("over http/1.1", Encoding.UTF8.GetString(buffer, 0, result.Count));

        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, default).WaitAsync(Timeout);
    }

    [Fact]
    public async Task RejectionsAreSentAsHttp2Responses()
    {
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var server = new ServerBuilder(serviceProvider)
            .ListenHttp2WebSocket(new IPEndPoint(IPAddress.Loopback, 0), builder => builder.Run(async connection =>
            {
                var handshake = new WebSocketServerProtocol(connection);
                await handshake.ReadRequestAsync();
                await handshake.RejectAsync(403);
                await connection.DisposeAsync();
            }))
            .Build();

        await server.StartAsync();
        try
        {
            var port = ((IPEndPoint)server.EndPoints.Single()).Port;
            using var invoker = CreateInvoker();
            var ex = await Assert.ThrowsAsync<WebSocketHandshakeException>(() =>
                WebSocketHttp2Client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), new WebSocketClientOptions(), invoker).AsTask().WaitAsync(Timeout));
            Assert.Equal(403, ex.StatusCode);
        }
        finally
        {
            await server.StopAsync();
        }
    }

    #region Helpers

    private sealed class EchoHost : IAsyncDisposable
    {
        public Server Server { get; init; }
        public int Port => ((IPEndPoint)Server.EndPoints.Single()).Port;
        public TaskCompletionSource<string> LastTarget { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> LastVersion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask DisposeAsync() => await Server.StopAsync();
    }

    private static async Task<EchoHost> StartEchoServerAsync(Action<Http2WebSocketListenerOptions> configureListener, bool compress, bool tls)
    {
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        EchoHost host = null;

        var server = new ServerBuilder(serviceProvider)
            .ListenHttp2WebSocket(
                tls ? new UriEndPoint(new Uri("wss://127.0.0.1:0")) : new IPEndPoint(IPAddress.Loopback, 0),
                builder => builder.RunWebSocket(async (session, request) =>
                {
                    host.LastVersion.TrySetResult(request.HttpVersion);
                    host.LastTarget.TrySetResult(request.RequestTarget);
                    while (true)
                    {
                        var message = await session.ReadMessageAsync();
                        if (message.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }
                        await session.SendAsync(message.Data, message.MessageType);
                    }
                }, options =>
                {
                    options.KeepAliveInterval = TimeSpan.Zero;
                    options.SubProtocols.Add("echo.v1");
                    if (compress)
                    {
                        options.Compression = new WebSocketCompressionOptions();
                    }
                }),
                configureListener)
            .Build();

        host = new EchoHost { Server = server };
        await server.StartAsync();
        return host;
    }

    private static HttpMessageInvoker CreateInvoker() => new(new SocketsHttpHandler
    {
        SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true },
    });

    private static async Task AssertEchoAsync(WebSocketSession session)
    {
        foreach (var size in new[] { 0, 1, 1000, 200_000 })
        {
            var payload = new byte[size];
            new Random(size).NextBytes(payload.AsSpan(0, size / 2));
            await session.SendAsync(payload, WebSocketMessageType.Binary).AsTask().WaitAsync(Timeout);
            var message = await session.ReadMessageAsync().AsTask().WaitAsync(Timeout);
            Assert.Equal(WebSocketMessageType.Binary, message.MessageType);
            Assert.Equal(payload, message.Data.ToArray());
        }

        await session.SendTextAsync("text ☃").AsTask().WaitAsync(Timeout);
        Assert.Equal("text ☃", (await session.ReadMessageAsync().AsTask().WaitAsync(Timeout)).GetText());
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        san.AddDnsName("localhost");
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));

        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null);
#else
        return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
#endif
    }

    #endregion
}
