using System;
using System.Collections.Generic;
using System.Text;
using static Bedrock.Framework.Experimental.Protocols.Tls.Interop.LibCrypto.LibCrypto;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Hashs
{
    internal class OpenSslHash : Hash
    {
        private readonly HashType _hashType;
        private readonly int _size;
        private EVP_MD_CTX _ctx;

        internal OpenSslHash(EVP_HashType hashTypePointer, int size, HashType hashType)
        {
            _hashType = hashType;
            _ctx = EVP_MD_CTX_new(hashTypePointer);
            _size = size;
        }

        public override int HashSize => _size;
        public override HashType HashType => _hashType;

        public override void HashData(ReadOnlySpan<byte> data) => EVP_DigestUpdate(_ctx, data);

        public override int Finish(Span<byte> output)
        {
            var result = EVP_DigestFinal_ex(_ctx, output);
            Dispose();
            return result;
        }

        public override int InterimHash(Span<byte> output)
        {
            var ctx = EVP_MD_CTX_copy_ex(_ctx);
            try
            {
                return EVP_DigestFinal_ex(ctx, output);
            }
            finally
            {
                ctx.Free();
            }
        }

        public void Dispose()
        {
            _ctx.Free();
            GC.SuppressFinalize(this);
        }

        ~OpenSslHash() => Dispose();
    }
}
