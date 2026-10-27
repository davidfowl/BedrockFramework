using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Bedrock.Framework.Experimental.Protocols.WebSockets;

namespace ServerApplication;

/// <summary>
/// A WebSocket echo server built on the native Bedrock WebSocket implementation.
/// Try it with the client sample, a browser (new WebSocket("ws://localhost:5006/ws")) or any WebSocket client.
/// </summary>
public static class WebSocketApplication
{
    public static void Configure(WebSocketServerOptions options)
    {
        options.SubProtocols.Add("echo");
        options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        options.KeepAliveTimeout = TimeSpan.FromSeconds(20);
        options.Compression = new WebSocketCompressionOptions();
    }

    public static async Task RunAsync(WebSocketSession session, WebSocketUpgradeRequest request)
    {
        if (request.Path != "/ws")
        {
            await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Unknown path");
            return;
        }

        Console.WriteLine($"WebSocket connected: {request.Path} (subprotocol: {session.SubProtocol ?? "none"}, compression: {session.Compression?.ToString() ?? "none"})");

        while (true)
        {
            var message = await session.ReadMessageAsync();
            if (message.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine($"WebSocket closed: {message.CloseStatus} {message.CloseStatusDescription}");
                return;
            }

            await session.SendAsync(message.Data, message.MessageType);
        }
    }
}
