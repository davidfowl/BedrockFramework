using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    /// <summary>
    /// A WebSocket frame to write as output.
    /// </summary>
    public struct WebSocketWriteFrame
    {
        /// <summary>
        /// The header of the WebSocket frame.
        /// </summary>
        public WebSocketHeader Header { get; set; }

        /// <summary>
        /// The payload of the WebSocket frame.
        /// </summary>
        public ReadOnlySequence<byte> Payload { get; set; }

        /// <summary>
        /// Whether or not the payload sequence has already been masked
        /// for delivery, if necessary.
        /// </summary>
        internal bool MaskingComplete;
    }
}
