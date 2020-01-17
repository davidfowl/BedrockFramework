using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.BulkCiphers
{
    internal abstract class BulkCipherProvider
    {
        public abstract BulkCipher GetCipher<T>(BulkCipherType cipherType, ReadOnlyMemory<byte> keyStorage) where T : BulkCipher, new();
        public abstract BulkCipherKey GetCipherKey(BulkCipherType cipherType, Memory<byte> keyStorage);
        public abstract (int keySize, int ivSize) GetCipherSize(BulkCipherType cipherType);
    }
}
