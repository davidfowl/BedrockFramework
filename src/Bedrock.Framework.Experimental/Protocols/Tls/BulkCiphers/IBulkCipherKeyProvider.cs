using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.BulkCiphers
{
    internal interface IBulkCipherKeyProvider : IDisposable
    {
        T GetCipher<T>(BulkCipherType cipherType, ReadOnlyMemory<byte> keyStorage) where T : AeadBulkCipher, new();
        ISymmetricalCipher GetCipherKey(BulkCipherType cipherType, ReadOnlyMemory<byte> keyStorage);
        (int keySize, int ivSize) GetCipherSize(BulkCipherType cipherType);
    }
}
