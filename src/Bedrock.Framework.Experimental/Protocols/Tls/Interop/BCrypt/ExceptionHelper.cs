using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt
{
    internal static partial class BCrypt
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerNonUserCode]
        internal static void ThrowOnErrorReturnCode(NTSTATUS returnCode)
        {
            if (returnCode != 0)
            {
                throw new InvalidOperationException($"Api Error {returnCode}");
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerStepThrough]
        internal static void ThrowException(Exception ex) => throw ex;
    }
}
