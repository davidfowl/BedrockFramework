using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.LibCrypto
{
    internal static partial class LibCrypto
    {
        [DllImport(Libraries.LibCrypto, CallingConvention = CallingConvention.Cdecl, EntryPoint = nameof(EVP_MD_CTX_copy_ex))]
        private static extern int EVP_MD_CTX_copy_ex_internal(EVP_MD_CTX copy, EVP_MD_CTX original);

        public static EVP_MD_CTX EVP_MD_CTX_copy_ex(EVP_MD_CTX original)
        {
            var copy = EVP_MD_CTX_new();
            var result = EVP_MD_CTX_copy_ex_internal(copy, original);
            ThrowOnErrorReturnCode(result);
            return copy;
        }
    }
}
