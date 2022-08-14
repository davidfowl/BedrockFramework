using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// The type of the WebSocket protocol, whether it be client or server.
    /// </summary>
    public enum WebSocketProtocolType
    {
        /// <summary>
        /// A WebSocket client. Frames sent to the server will be masked.
        /// </summary>
        Client,

        /// <summary>
        /// A WebSocket server. Frames sent to the client will not be masked.
        /// </summary>
        Server
    }
}
