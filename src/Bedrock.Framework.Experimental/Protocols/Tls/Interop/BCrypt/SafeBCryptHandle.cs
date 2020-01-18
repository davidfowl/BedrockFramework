using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt
{
    internal abstract class SafeBCryptHandle : SafeHandle, IDisposable
    {
        protected SafeBCryptHandle() : base(IntPtr.Zero, true) { }

        public sealed override bool IsInvalid => handle == IntPtr.Zero;

        protected abstract override bool ReleaseHandle();
    }
}
