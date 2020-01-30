using System;
using System.Runtime.Serialization;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// An exception thrown when the WebSocket protocol is violated.
    /// </summary>
    [Serializable]
    public class WebSocketProtocolException : Exception
    {
        /// <summary>
        /// Creates an instance of a WebSocketProtocolException.
        /// </summary>
        /// <param name="message">The message containing the description of the protocol error.</param>
        public WebSocketProtocolException(string message) : base(message) { }

        /// <summary>
        /// Creates an instance of a WebSocketProtocolException.
        /// </summary>
        /// <param name="info">The info object required to serialize the exception.</param>
        /// <param name="context">The streaming context required to serialize the exception.</param>
        protected WebSocketProtocolException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
