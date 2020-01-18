using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt
{
    internal sealed class SafeBCryptHashHandle : SafeBCryptHandle
    {
        private SafeBCryptHashHandle() : base() { }

        protected sealed override bool ReleaseHandle()
        {
            var ntStatus = BCrypt.BCryptDestroyHash(handle);
            return ntStatus == NTSTATUS.STATUS_SUCCESS;
        }
    }
}
