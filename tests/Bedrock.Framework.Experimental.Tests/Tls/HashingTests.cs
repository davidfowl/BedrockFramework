using Bedrock.Framework.Experimental.Protocols.Tls.Hashs;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Bedrock.Framework.Experimental.Tests.Tls
{
    public class HashingTests
    {
        private static readonly OpenSslHashProvider _provider = new OpenSslHashProvider();
        private static readonly byte[] _data = GetRandomData();

        private static byte[] CoreFxHash(HashType hashType) => CoreFxHash(hashType, _data);

        private static byte[] CoreFxHash(HashType hashType, Span<byte> data)
        {
            System.Security.Cryptography.HashAlgorithm hash = hashType switch
            {
                HashType.SHA256 => System.Security.Cryptography.SHA256.Create(),
                HashType.SHA384 => System.Security.Cryptography.SHA384.Create(),
                HashType.SHA512 => System.Security.Cryptography.SHA512.Create(),
                _ => throw new InvalidOperationException(),
            };
            return hash.ComputeHash(data.ToArray());
        }

        private static byte[] GetRandomData()
        {
            var random = new Random();
            var data = new byte[1024];
            random.NextBytes(data);
            return data;
        }

        [Theory()]
        [InlineData(HashType.SHA256)]
        [InlineData(HashType.SHA384)]
        [InlineData(HashType.SHA512)]
        public void OpenSslBlockHashing(HashType hashType)
        {
            using var hash = _provider.GetHash(hashType);
            var result = new byte[hash.HashSize];
            hash.HashData(_data);
            hash.Finish(result);

            Assert.Equal(CoreFxHash(hashType), result);
        }

        [Theory()]
        [InlineData(HashType.SHA256)]
        [InlineData(HashType.SHA384)]
        [InlineData(HashType.SHA512)]
        public void OpenSslStreamHashing(HashType hashType)
        {
            using var hash = _provider.GetHash(hashType);
            var dataSpan = _data.AsSpan();
            hash.HashData(dataSpan.Slice(0, 200));
            hash.HashData(dataSpan.Slice(200));

            var result = new byte[hash.HashSize];
            hash.Finish(result);

            Assert.Equal(CoreFxHash(hashType), result);
        }

        [Theory()]
        [InlineData(HashType.SHA256)]
        [InlineData(HashType.SHA384)]
        [InlineData(HashType.SHA512)]
        public void OpenSslInterimHashing(HashType hashType)
        {
            using var hash = _provider.GetHash(hashType);
            var dataSpan = _data.AsSpan();

            var block1 = dataSpan.Slice(0, 100);
            var block2 = dataSpan.Slice(100);

            hash.HashData(block1);

            var result = new byte[hash.HashSize];
            hash.InterimHash(result);

            Assert.Equal(CoreFxHash(hashType, block1), result);

            hash.HashData(block2);
            hash.Finish(result);
            Assert.Equal(CoreFxHash(hashType), result);
        }
    }
}
