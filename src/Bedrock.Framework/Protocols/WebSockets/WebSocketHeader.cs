using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// A header for WebSocket frames.
    /// </summary>
    public readonly struct WebSocketHeader : IEquatable<WebSocketHeader>
    {
        /// <summary>
        /// An instance of a thread-safe and cryptographically sound random number generator.
        /// </summary>
        private readonly static RandomNumberGenerator _rng = RandomNumberGenerator.Create();

        /// <summary>
        /// Whether or not this is the final frame in the message.
        /// </summary>
        public bool Fin { get; }

        /// <summary>
        /// The opcode of the frame.
        /// </summary>
        public WebSocketOpcode Opcode { get; }

        /// <summary>
        /// Whether the frame payload is masked.
        /// </summary>
        public bool Masked { get; }

        /// <summary>
        /// The length of the frame payload.
        /// </summary>
        public ulong PayloadLength { get; }

        /// <summary>
        /// The masking key used to unmask the payload, if masked.
        /// </summary>
        public int MaskingKey { get; }

        /// <summary>
        /// Creates an instance of a WebSocketHeader.
        /// </summary>
        /// <param name="fin">Whether or not this is the final frame in the message.</param>
        /// <param name="opcode">The opcode of the frame.</param>
        /// <param name="masked">Whether the frame payload is masked.</param>
        /// <param name="payloadLength">The length of the frame payload.</param>
        /// <param name="maskingKey">The masking key used to unmask the payload, if masked. This mask must be a cryptographically random value.</param>
        public WebSocketHeader(bool fin, WebSocketOpcode opcode, bool masked, ulong payloadLength, int maskingKey)
        {
            Fin = fin;
            Opcode = opcode;
            Masked = masked;
            PayloadLength = payloadLength;
            MaskingKey = maskingKey;
        }

        /// <summary>
        /// Generates a random masking key.
        /// </summary>
        /// <returns>A random four byte masking key, as an int.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GenerateMaskingKey()
        {
            Span<byte> keyBytes = stackalloc byte[4];
            _rng.GetBytes(keyBytes);

            return BitConverter.ToInt32(keyBytes);
        }

        /// <summary>
        /// Creates a WebSocketHeader with a random masking key that indicates a masked payload.
        /// </summary>
        /// <param name="fin">Whether or not this is the final frame in the message.</param>
        /// <param name="opcode">The opcode of the frame.</param>
        /// <param name="payloadLength">The length of the frame payload.</param>
        /// <returns>A new WebSocketHeader.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebSocketHeader CreateMasked(bool fin, WebSocketOpcode opcode, ulong payloadLength)
        {
            return new WebSocketHeader(fin, opcode, true, payloadLength, GenerateMaskingKey());
        }

        /// <summary>
        /// Creates a WebSocketHeader that indicates no payload masking.
        /// </summary>
        /// <param name="fin">Whether or not this is the final frame in the message.</param>
        /// <param name="opcode">The opcode of the frame.</param>
        /// <param name="payloadLength">The length of the frame payload.</param>
        /// <returns>A new WebSocketHeader.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WebSocketHeader CreateUnmasked(bool fin, WebSocketOpcode opcode, ulong payloadLength)
        {
            return new WebSocketHeader(fin, opcode, false, payloadLength, default);
        }

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
        /// Checks to see if a WebSocketHeader value is equal to this value.	
        /// </summary>
        /// <param name="other">The WebSocketHeader value to check against.</param>
        /// <returns>True if equal, false otherwise.</returns>
        bool IEquatable<WebSocketHeader>.Equals(WebSocketHeader other) => this == other;

        /// <summary>	
        /// Gets the hashcode of the WebSocketHeader value.	
        /// </summary>	
        /// <returns>The value hashcode.</returns>
        public override int GetHashCode() => HashCode.Combine(Fin, Opcode, Masked, PayloadLength, MaskingKey);

        /// <summary>
        /// Creates a string representation of the WebSocketHeader.
        /// </summary>
        /// <returns>A string representation of the WebSocketHeader.</returns>
        public override string ToString() => $"Fin: {Fin} | Opcode: {Opcode} | Masked: {Masked} | Payload Length: {PayloadLength} | Masking Key: {MaskingKey:X8}";
    }
}
