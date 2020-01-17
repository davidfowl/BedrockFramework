using Bedrock.Framework.Experimental.Protocols.Tls.Certificates;
using Bedrock.Framework.Experimental.Protocols.Tls.Hashs;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.KeyExchanges
{
    internal abstract class KeyExchange : IDisposable
    {
        public abstract bool RequiresServerKeyExchange { get; }
        public abstract void SetPeerKey(ReadOnlySequence<byte> peerKey, Certificate certificate, SignatureScheme scheme);
        public abstract void SetPeerKey(ReadOnlySequence<byte> sharedKey);
        public abstract int KeyExchangeSize { get; }
        public abstract int WritePublicKey(Span<byte> keyBuffer);
        public abstract NamedGroup NamedGroup { get; }
        public abstract void DeriveSecret(HashProvider hashProvider, HashType hashType, ReadOnlySpan<byte> salt, Span<byte> output);
        public abstract void DeriveMasterSecret(HashProvider hashProvider, HashType hashType, ReadOnlySpan<byte> seed, Span<byte> output);
        public abstract void Dispose();

        public void SetPeerKey(SequenceReader<byte> peerKey, Certificate certificate, SignatureScheme scheme)
        {
            var buffer = peerKey.Sequence.Slice(peerKey.Position);
            SetPeerKey(buffer, certificate, scheme);
        }
    }
}
