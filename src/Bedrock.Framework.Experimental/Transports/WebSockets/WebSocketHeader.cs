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

        /// <summary>
        /// Checks two WebSocketHeader values for equality.
        /// </summary>
        /// <param name="x">The value to check.</param>
        /// <param name="y">The value to check against.</param>
        /// <returns>True if equal, false otherwise.</returns>
        public static bool operator ==(WebSocketHeader x, WebSocketHeader y) =>
            x.Fin == y.Fin
            && x.Opcode == y.Opcode
            && x.Masked == y.Masked
            && x.PayloadLength == y.PayloadLength
            && x.MaskingKey == y.MaskingKey;

        /// <summary>
        /// Checks two WebSocketHeader values for inequality.
        /// </summary>
        /// <param name="x">The value to check.</param>
        /// <param name="y">The value to check against.</param>
        /// <returns>True if unequal, false otherwise.</returns>
        public static bool operator !=(WebSocketHeader x, WebSocketHeader y) =>
            x.Fin != y.Fin
            || x.Opcode != y.Opcode
            || x.Masked != y.Masked
            || x.PayloadLength != y.PayloadLength
            || x.MaskingKey != y.MaskingKey;

        /// <summary>
        /// Checks to see if a WebSocketHeader value is equal to this value.
        /// </summary>
        /// <param name="obj">The WebSocketHeader value to check against.</param>
        /// <returns>True if equal, false otherwise.</returns>
        public override bool Equals(object obj) =>
            obj is object
            && obj is WebSocketHeader
            && (WebSocketHeader)obj == this;

        /// <summary>
        /// Gets the hashcode of the WebSocketHeader value.
        /// </summary>
        /// <returns>The value hashcode.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return 17
                    * (23 + Fin.GetHashCode())
                    * (23 + Opcode.GetHashCode())
                    * (23 + Masked.GetHashCode())
                    * (23 + PayloadLength.GetHashCode())
                    * (23 + MaskingKey.GetHashCode());
            }
        }
    }
}