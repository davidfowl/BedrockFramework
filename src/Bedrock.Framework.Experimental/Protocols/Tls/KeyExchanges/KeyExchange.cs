using Bedrock.Framework.Experimental.Protocols.Tls.Certificates;
using Bedrock.Framework.Experimental.Protocols.Tls.Hashs;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.KeyExchanges
{
    internal abstract class KeyExchange : IDisposable
    {
        public abstract bool RequiresServerKeyExchange { get; }
        public abstract void SetPeerKey(ReadOnlySequence<byte> peerKey, X509Certificate2 certificate, SignatureScheme scheme);
        public abstract void SetPeerKey(ReadOnlySequence<byte> sharedKey);
        public abstract int KeyExchangeSize { get; }
        public abstract int WritePublicKey(Span<byte> keyBuffer);
        public abstract NamedGroup NamedGroup { get; }
        public abstract void DeriveSecret(HashProvider hashProvider, HashType hashType, ReadOnlySpan<byte> salt, Span<byte> output);
        public abstract void DeriveMasterSecret(HashProvider hashProvider, HashType hashType, ReadOnlySpan<byte> seed, Span<byte> output);
        public abstract void Dispose();

        public void SetPeerKey(SequenceReader<byte> peerKey, X509Certificate2 certificate, SignatureScheme scheme)
        {
            var buffer = peerKey.Sequence.Slice(peerKey.Position);
            SetPeerKey(buffer, certificate, scheme);
        }
    }
}
