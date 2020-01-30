using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// The status of the closing of a WebSocket.
    /// </summary>
    public enum WebSocketCloseStatus : short
    {
        /// <summary>
        /// A normal closure, for which the purpose for the connection has been fulfilled.
        /// </summary>
        Normal = 1000,

        /// <summary>
        /// Indicates that an endpoint is going away, such as a server going down or a
        /// browser navigating away.
        /// </summary>
        GoingAway = 1001,

        /// <summary>
        /// Indicates that an endpoint is terminating the connection due to a protocol error.
        /// </summary>
        ProtocolError = 1002,

        /// <summary>
        /// Indicates that an endpoint is terminating the connection because it has received 
        /// a type of data it cannot accept.
        /// </summary>
        UnacceptableData = 1003,

        /// <summary>
        /// Indicates that an endpoint is terminating the connection because it has received 
        /// data within a message that was not consistent with the type of the message.
        /// </summary>
        IncorrectDataType = 1007,

        /// <summary>
        /// Indicates that an endpoint is terminating the connection because it has received 
        /// a message that violates its policy.
        /// </summary>
        PolicyViolation = 1008,

        /// <summary>
        /// Indicates that an endpoint is terminating the connection because it has received
        /// a message that is too big for it to process.
        /// </summary>
        MessageTooLarge = 1009,

        /// <summary>
        /// Indicates that an endpoint (client) is terminating the connection because it has
        /// expected the server to negotiate one or more extensions, but the server didn't 
        /// return them in the response message of the WebSocket handshake.
        /// </summary>
        ExpectedExtensionNotFound = 1010,

        /// <summary>
        /// Indicates that a server is terminating the connection because it encountered 
        /// an unexpected condition that prevented it from fulfilling the request.
        /// </summary>
        UnexpectedError = 1011
    }
}
