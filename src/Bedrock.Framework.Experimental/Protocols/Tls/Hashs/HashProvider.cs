using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Hashs
{
    public abstract class HashProvider
    {
        public abstract int HashSize(HashType hashType);
        public abstract Hash GetHash(HashType hashType);
        public abstract void HmacData(HashType hashType, ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> result);
    }
}
