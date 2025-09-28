using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// A result from reading from a WebSocket message.
    /// </summary>
    public readonly struct MessageReadResult
    {
        /// <summary>
        /// True if the data is the final data in the message, false otherwise.
        /// </summary>
        public bool IsEndOfMessage { get; }

        /// <summary>
        /// True if the underlying transport read was canceled, false otherwise.
        /// </summary>
        public bool IsCanceled { get; }

        /// <summary>
        /// True if the underlying transport is completed, false otherwise.
        /// </summary>
        public bool IsCompleted { get; }

        /// <summary>
        /// The data read from the WebSocket.
        /// </summary>
        public ReadOnlySequence<byte> Data { get; }

        /// <summary>
        /// Creates an instance of a MessageReadResult.
        /// </summary>
        /// <param name="data">The data read from the WebSocket.</param>
        /// <param name="isEndOfMessage">True if the data is the final data in the message, false otherwise.</param>
        /// <param name="isCanceled">True if the underlying transport read was canceled, false otherwise.</param>
        /// <param name="isCompleted">True if the underlying transport is completed, false otherwise.</param>
        public MessageReadResult(ReadOnlySequence<byte> data, bool isEndOfMessage, bool isCanceled, bool isCompleted)
        {
            Data = data;
            IsEndOfMessage = isEndOfMessage;
            IsCanceled = isCanceled;
            IsCompleted = isCompleted;
        }
    }
}
