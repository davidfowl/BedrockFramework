using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.LibCrypto
{
    internal static partial class LibCrypto
    {
        [DllImport(Libraries.LibCrypto, CallingConvention = CallingConvention.Cdecl)]
        private static extern EVP_MD_CTX EVP_MD_CTX_new();

        public static EVP_MD_CTX EVP_MD_CTX_new(EVP_HashType hashType)
        {
            var ctx = EVP_MD_CTX_new();
            var result = EVP_DigestInit_ex(ctx, hashType, IntPtr.Zero);
            ThrowOnErrorReturnCode(result);
            return ctx;
        }
    }
}
