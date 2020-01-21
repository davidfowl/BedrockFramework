using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    /// <summary>
    /// A WebSocket frame to read as input.
    /// </summary>
    public readonly struct WebSocketReadFrame
    {
        /// <summary>
        /// The header of the WebSocket frame.
        /// </summary>
        public WebSocketHeader Header { get; }

        /// <summary>
        /// A message reader for reading the WebSocket payload.
        /// </summary>
        public WebSocketPayloadReader Payload { get; }

        /// <summary>
        /// Creates an instance of a WebSocketReadFrame.
        /// </summary>
        /// <param name="header">The header of the WebSocket frame.</param>
        /// <param name="payload">A message reader for reading the WebSocket payload.</param>
        public WebSocketReadFrame(WebSocketHeader header, WebSocketPayloadReader payload)
        {
            Header = header;
            Payload = payload;
        }
    }
}
