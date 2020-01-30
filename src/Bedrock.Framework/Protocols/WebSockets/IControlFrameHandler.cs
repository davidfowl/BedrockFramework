using Bedrock.Framework.Protocols.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// Handles WebSocket control frames encountered by a WebSocketMessageReader.
    /// </summary>
    public interface IControlFrameHandler
    {
        /// <summary>
        /// Handles a WebSocket control frame.
        /// </summary>
        /// <param name="controlFrame">The control frame to handle.</param>
        /// <param name="cancellationToken">A cancellation token, if any.</param>
        ValueTask HandleControlFrameAsync(WebSocketControlFrame controlFrame, CancellationToken cancellationToken = default);
    }
}