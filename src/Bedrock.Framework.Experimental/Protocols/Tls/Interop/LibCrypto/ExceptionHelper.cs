using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.LibCrypto
{
    internal static partial class LibCrypto
    {
        [System.Diagnostics.DebuggerHidden()]
        internal static int ThrowOnErrorReturnCode(int returnCode)
        {
            if (returnCode != 1) ThrowSecurityException();
            return returnCode;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [System.Diagnostics.DebuggerHidden()]
        internal static unsafe void ThrowSecurityException()
        {
            //512 is defined in openssl as the maximum buffer size needed
            var tempBuffer = new byte[512];
            fixed (byte* buffPointer = tempBuffer)
            {
                var errCode = ERR_get_error();
                ERR_error_string_n(errCode, buffPointer, (UIntPtr)tempBuffer.Length);
                var errorString = Marshal.PtrToStringAnsi((IntPtr)buffPointer);
                throw new System.Security.SecurityException($"{errCode}-{errorString}");
            }
        }

        [System.Diagnostics.DebuggerHidden()]
        internal static unsafe void* ThrowOnNullPointer(void* ptr)
        {
            if (ptr == null) ThrowSecurityException();
            return ptr;
        }

        [System.Diagnostics.DebuggerHidden()]
        internal static IntPtr ThrowOnError(IntPtr returnCode)
        {
            if (returnCode.ToInt64() < 1) ThrowSecurityException();
            return returnCode;
        }
    }
}
