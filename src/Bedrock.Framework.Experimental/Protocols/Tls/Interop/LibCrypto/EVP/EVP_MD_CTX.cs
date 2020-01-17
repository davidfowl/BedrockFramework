using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.LibCrypto
{
    internal static partial class LibCrypto
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct EVP_MD_CTX
        {
            private IntPtr _ptr;

            public void Free()
            {
                if (_ptr == IntPtr.Zero) return;
                EVP_MD_CTX_free(_ptr);
                _ptr = IntPtr.Zero;
            }
        }
    }
}
