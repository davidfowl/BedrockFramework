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
               
        [Fact]
        public void OpenSslNormalHashing()
        {
            var random = new Random();
            var data = new byte[1024];
            random.NextBytes(data);
            
            var hash = _provider.GetHash(HashType.SHA256);
            var coreFxHash = System.Security.Cryptography.SHA256.Create();
            var coreFxResult = coreFxHash.ComputeHash(data);
            var result = new byte[coreFxResult.Length];
            hash.HashData(data);
            hash.Finish(result);

            Assert.Equal(coreFxResult, result);
        }
    }
}
