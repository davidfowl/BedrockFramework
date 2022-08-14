using Bedrock.Framework.Protocols.WebSockets;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// A WebSocket control frame.
    /// </summary>
    public class WebSocketControlFrame
    {
        /// <summary>
        /// The opcode of the control frame.
        /// </summary>
        public WebSocketOpcode Opcode { get; }

        /// <summary>
        /// The close status, if provided and if the frame is a close frame.
        /// </summary>
        public WebSocketCloseStatus CloseStatus { get; }

        /// <summary>
        /// The payload of the control frame.
        /// </summary>
        public ReadOnlySequence<byte> Payload { get; }

        /// <summary>
        /// Creates an instance of a WebSocketControlFrame.
        /// </summary>
        /// <param name="opcode">The opcode of the control frame.</param>
        /// <param name="closeStatus">The close status, if provided and if the frame is a close frame.</param>
        /// <param name="payload">The payload of the control frame.</param>
        public WebSocketControlFrame(WebSocketOpcode opcode, WebSocketCloseStatus closeStatus = default, ReadOnlySequence<byte> payload = default)
        {
            Opcode = opcode;
            CloseStatus = closeStatus;
            Payload = payload;
        }
    }
}
