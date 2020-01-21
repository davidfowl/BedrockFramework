using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    /// <summary>
    /// The opcode of a WebSocket message frame header.
    /// </summary>
    public enum WebSocketOpcode : byte
    {
        /// <summary>
        /// A message containing the continuation of the payload of a previous frame.
        /// </summary>
        Continuation = 0x0,

        /// <summary>
        /// A message frame containing a text message.
        /// </summary>
        Text = 0x1,

        /// <summary>
        /// A message frame containing a binary message.
        /// </summary>
        Binary = 0x2,

        /// <summary>
        /// A control message frame containing a socket close message.
        /// </summary>
        Close = 0x8,

        /// <summary>
        /// A control message frame containing a ping message.
        /// </summary>
        Ping = 0x9,

        /// <summary>
        /// A control message frame containing a pong message.
        /// </summary>
        Pong = 0xA
    }
}
