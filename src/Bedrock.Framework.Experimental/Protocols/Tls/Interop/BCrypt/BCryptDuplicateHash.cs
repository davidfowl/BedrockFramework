using System;
using System.Runtime.InteropServices;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt
{
    internal static partial class BCrypt
    {
        [DllImport(Libraries.BCrypt, CharSet = CharSet.Unicode)]
        private extern static NTSTATUS BCryptDuplicateHash(SafeBCryptHashHandle hHash, out SafeBCryptHashHandle phNewHash, IntPtr pbHashObject, int cbHashObject, int dwFlags);

        internal static SafeBCryptHashHandle BCryptDuplicateHash(SafeBCryptHashHandle handle)
        {
            var result = BCryptDuplicateHash(handle, out var newhandle, IntPtr.Zero, 0, 0);
            ThrowOnErrorReturnCode(result);
            return newhandle;
        }
    }
}
