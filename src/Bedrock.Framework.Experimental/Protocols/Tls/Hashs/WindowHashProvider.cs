using Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt;
using System;
using System.Collections.Generic;
using System.Text;
using static Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt.BCrypt;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Hashs
{
    public sealed class WindowsHashProvider : HashProvider
    {
        private (SafeBCryptAlgorithmHandle hash, SafeBCryptAlgorithmHandle hmac) _sha256;
        private (SafeBCryptAlgorithmHandle hash, SafeBCryptAlgorithmHandle hmac) _sha384;
        private (SafeBCryptAlgorithmHandle hash, SafeBCryptAlgorithmHandle hmac) _sha512;

        public WindowsHashProvider()
        {
            _sha256 = BCryptOpenAlgorithmHashProvider(HashType.SHA256.ToString());
            _sha384 = BCryptOpenAlgorithmHashProvider(HashType.SHA384.ToString());
            _sha512 = BCryptOpenAlgorithmHashProvider(HashType.SHA512.ToString());
        }

        public override Hash GetHash(HashType hashType)
        {
            var (handle, hmac, size) = GetHashType(hashType);
            return new WindowsHash(handle, size, hashType);
        }

        public override int HashSize(HashType hashType) => GetHashType(hashType).size;

        private (SafeBCryptAlgorithmHandle handle, SafeBCryptAlgorithmHandle hmac, int size) GetHashType(HashType hashType)
        {
            switch (hashType)
            {
                case HashType.SHA256:
                    return (_sha256.hash, _sha256.hmac, 256 / 8);
                case HashType.SHA384:
                    return (_sha384.hash, _sha384.hmac, 384 / 8);
                case HashType.SHA512:
                    return (_sha512.hash, _sha512.hmac, 512 / 8);
                default:
                    ThrowException(new InvalidOperationException());
                    return (null, null, default);
            }
        }

        public override void HmacData(HashType hashType, ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> result)
        {
            var handle = GetHashType(hashType).hmac;
            BCryptHash(handle, key, message, result);
        }
    }
}
