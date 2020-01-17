using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    /// <summary>
    /// An exception thrown when WebSocket frame data is in error.
    /// </summary>
    public class WebSocketFrameException : Exception
    {
        /// <summary>
        /// Creates an instance of a WebSocketFrameException.
        /// </summary>
        /// <param name="message">The message containing the description of the frame error.</param>
        public WebSocketFrameException(string message) : base(message) { }
    }
}
