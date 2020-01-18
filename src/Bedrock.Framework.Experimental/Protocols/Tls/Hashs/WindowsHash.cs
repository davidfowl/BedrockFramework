using System;
using System.Collections.Generic;
using System.Text;
using static Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt.BCrypt;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Hashs
{
    public sealed class WindowsHash : Hash
    {
        private SafeBCryptHashHandle _hashHandle;
        private readonly int _size;
        private readonly HashType _hashType;

        public WindowsHash(SafeBCryptAlgorithmHandle algoHandle, int size, HashType hashType)
        {
            _hashType = hashType;
            _size = size;
            _hashHandle = BCryptCreateHash(algoHandle);
        }

        public override int HashSize => _size;
        public override HashType HashType => _hashType;

        public override int Finish(Span<byte> output)
        {
            using (_hashHandle)
            {
                BCryptFinishHash(_hashHandle, output);
            }
            _hashHandle = null;
            return _size;
        }

        public override void HashData(ReadOnlySpan<byte> data) => BCryptHashData(_hashHandle, data);

        public override int InterimHash(Span<byte> output)
        {
            using (var newHash = BCryptDuplicateHash(_hashHandle))
            {
                BCryptFinishHash(newHash, output);
                return _size;
            }
        }

        protected override void Dispose(bool disposing)
        {
            _hashHandle?.Dispose();
            _hashHandle = null;
        }
    }
}
