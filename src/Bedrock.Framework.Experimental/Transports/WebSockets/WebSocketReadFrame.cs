using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    /// <summary>
    /// A WebSocket frame to read as input.
    /// </summary>
    public struct WebSocketReadFrame
    {
        /// <summary>
        /// The header of the WebSocket frame.
        /// </summary>
        public WebSocketHeader Header { get; set; }

        /// <summary>
        /// A message reader for reading the WebSocket payload.
        /// </summary>
        public WebSocketPayloadReader Payload { get; set; }
    }
}
