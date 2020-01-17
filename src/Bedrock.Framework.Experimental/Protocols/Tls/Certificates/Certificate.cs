using Bedrock.Framework.Experimental.Protocols.Tls.Hashs;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Certificates
{
    internal abstract class Certificate
    {
        public abstract CertificateType CertificateType { get; }
        public abstract byte[] CertificateData { get; }
        public abstract byte[][] CertificateChain { get; }
        public abstract int SignatureSize { get; }
        public abstract SignatureScheme SelectAlgorithm(ReadOnlySequence<byte> buffer);
        public abstract bool SupportsScheme(SignatureScheme scheme);
        internal abstract int SignHash(HashProvider provider, SignatureScheme scheme, Span<byte> message, Span<byte> output);
        public abstract int Decrypt(SignatureScheme scheme, ReadOnlySpan<byte> encryptedData, Span<byte> output);
    }
}
