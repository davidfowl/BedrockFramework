using System;
using System.Collections.Generic;
using System.Text;
using static Bedrock.Framework.Experimental.Protocols.Tls.Interop.LibCrypto.LibCrypto;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Hashs
{
    public sealed class OpenSslHashProvider : HashProvider
    {
        public override void HmacData(HashType hashType, ReadOnlySpan<byte> key, ReadOnlySpan<byte> message, Span<byte> result)
        {
            var (type, _) = GetHashType(hashType);
            HMAC(type, key, message, result);
        }

        public override int HashSize(HashType hashType) => GetHashType(hashType).size;

        private static (EVP_HashType hash, int size) GetHashType(HashType hashType)
        {
            switch (hashType)
            {
                case HashType.SHA256:
                    return (EVP_sha256, 256 / 8);
                case HashType.SHA384:
                    return (EVP_sha384, 384 / 8);
                case HashType.SHA512:
                    return (EVP_sha512, 512 / 8);
                default:
                    throw new InvalidOperationException();
            }
        }

        public override Hash GetHash(HashType hashType)
        {
            var (type, size) = GetHashType(hashType);
            return new OpenSslHash(type, size, hashType);
        }
    }
}
