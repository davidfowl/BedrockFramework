using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// A result from reading from the WebSocket protocol.
    /// </summary>
    public readonly struct WebSocketReadResult
    {
        /// <summary>
        /// True if the message is UTF-8 text, false otherwise.
        /// </summary>
        public bool IsText { get; }

        /// <summary>
        /// The buffer returned from the read.
        /// </summary>
        public WebSocketMessageReader Reader { get; }

        /// <summary>
        /// Creates an instance of a WedSocketReadResult.
        /// </summary>
        /// <param name="isText">True if the message is UTF-8 text, false otherwise.</param>
        /// <param name="reader">The reader that will read until the end of the message.</param>
        public WebSocketReadResult(bool isText, WebSocketMessageReader reader)
        {
            IsText = isText;
            Reader = reader;
        }
    }
}
