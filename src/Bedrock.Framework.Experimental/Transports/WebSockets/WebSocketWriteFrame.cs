using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    /// <summary>
    /// A WebSocket frame to write as output.
    /// </summary>
    public class WebSocketWriteFrame
    {
        /// <summary>
        /// The header of the WebSocket frame.
        /// </summary>
        public WebSocketHeader Header { get; }

        /// <summary>
        /// The payload of the WebSocket frame.
        /// </summary>
        public ReadOnlySequence<byte> Payload { get; }

        /// <summary>
        /// Whether or not the payload sequence has already been masked
        /// for delivery, if necessary.
        /// </summary>
        internal bool MaskingComplete { get; set; }

        /// <summary>
        /// Creates an instance of a WebSocketWriteFrame.
        /// </summary>
        /// <param name="header">The header of the WebSocket frame.</param>
        /// <param name="payload">The payload of the WebSocket frame.</param>
        public WebSocketWriteFrame(WebSocketHeader header, ReadOnlySequence<byte> payload)
        {
            Header = header;
            Payload = payload;
        }
    }
}
