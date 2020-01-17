namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    /// <summary>
    /// A header for WebSocket frames.
    /// </summary>
    public struct WebSocketHeader
    {
        /// <summary>
        /// Whether or not this is the final frame in the message.
        /// </summary>
        public bool Fin;

        /// <summary>
        /// The opcode of the frame.
        /// </summary>
        public WebSocketOpcode Opcode;

        /// <summary>
        /// Whether the frame payload is masked.
        /// </summary>
        public bool Masked;

        /// <summary>
        /// The length of the frame payload.
        /// </summary>
        public ulong PayloadLength;

        /// <summary>
        /// The masking key used to unmask the payload, if masked.
        /// </summary>
        public int MaskingKey;
    }
}