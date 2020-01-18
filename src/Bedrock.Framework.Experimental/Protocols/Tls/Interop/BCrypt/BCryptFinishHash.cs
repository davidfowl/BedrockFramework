using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt
{
    internal partial class BCrypt
    {
        [DllImport(Libraries.BCrypt, CharSet = CharSet.Unicode)]
        private unsafe static extern NTSTATUS BCryptFinishHash(SafeBCryptHashHandle hHash, void* pbOutput, int cbOutput, int dwFlags);

        internal unsafe static void BCryptFinishHash(SafeBCryptHashHandle handle, Span<byte> output)
        {
            fixed (void* ptr = &MemoryMarshal.GetReference(output))
            {
                var result = BCryptFinishHash(handle, ptr, output.Length, 0);
                ThrowOnErrorReturnCode(result);
            }

        }
    }
}
