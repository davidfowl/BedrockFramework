using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt
{
    internal static partial class BCrypt
    {
        [DllImport(Libraries.BCrypt)]
        internal static extern NTSTATUS BCryptDestroyHash(IntPtr hHash);
    }
}
