using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt
{
    internal sealed class SafeBCryptAlgorithmHandle : SafeBCryptHandle
    {
        private SafeBCryptAlgorithmHandle() : base() { }

        protected sealed override bool ReleaseHandle()
        {
            var ntStatus = BCrypt.BCryptCloseAlgorithmProvider(handle, 0);
            return ntStatus == NTSTATUS.STATUS_SUCCESS;
        }
    }
}
