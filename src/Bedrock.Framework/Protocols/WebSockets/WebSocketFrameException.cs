using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// An exception thrown when WebSocket frame data is in error.
    /// </summary>
    [Serializable]
    public class WebSocketFrameException : Exception
    {
        /// <summary>
        /// Creates an instance of a WebSocketFrameException.
        /// </summary>
        /// <param name="message">The message containing the description of the frame error.</param>
        public WebSocketFrameException(string message) : base(message) { }
    }
}
