using System;
using System.Net.WebSockets;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// The result of a streaming <see cref="WebSocketSession.ReadAsync(Memory{byte}, System.Threading.CancellationToken)"/>.
/// </summary>
public readonly struct WebSocketReadResult
{
    public WebSocketReadResult(int count, WebSocketMessageType messageType, bool endOfMessage, WebSocketCloseStatus? closeStatus = null, string closeStatusDescription = null)
    {
        Count = count;
        MessageType = messageType;
        EndOfMessage = endOfMessage;
        CloseStatus = closeStatus;
        CloseStatusDescription = closeStatusDescription;
    }

    /// <summary>
    /// The number of bytes written into the caller's buffer.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// The type of the message being read. <see cref="WebSocketMessageType.Close"/> means the peer closed the session.
    /// </summary>
    public WebSocketMessageType MessageType { get; }

    /// <summary>
    /// Whether these bytes complete the current message.
    /// </summary>
    public bool EndOfMessage { get; }

    /// <summary>
    /// The close status received from the peer, when <see cref="MessageType"/> is Close. Null if the Close frame had no status.
    /// </summary>
    public WebSocketCloseStatus? CloseStatus { get; }

    /// <summary>
    /// The close reason received from the peer, when <see cref="MessageType"/> is Close.
    /// </summary>
    public string CloseStatusDescription { get; }
}

/// <summary>
/// A complete message returned by <see cref="WebSocketSession.ReadMessageAsync(System.Threading.CancellationToken)"/>.
/// </summary>
public sealed class WebSocketMessage
{
    public WebSocketMessage(WebSocketMessageType messageType, ReadOnlyMemory<byte> data, WebSocketCloseStatus? closeStatus = null, string closeStatusDescription = null)
    {
        MessageType = messageType;
        Data = data;
        CloseStatus = closeStatus;
        CloseStatusDescription = closeStatusDescription;
    }

    /// <summary>
    /// Text, Binary, or Close when the peer closed the session.
    /// </summary>
    public WebSocketMessageType MessageType { get; }

    /// <summary>
    /// The message payload. Empty for Close.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>
    /// The close status received from the peer, for Close messages.
    /// </summary>
    public WebSocketCloseStatus? CloseStatus { get; }

    /// <summary>
    /// The close reason received from the peer, for Close messages.
    /// </summary>
    public string CloseStatusDescription { get; }

    /// <summary>
    /// Decodes the payload as UTF-8.
    /// </summary>
    public string GetText() => Encoding.UTF8.GetString(Data.Span);
}
