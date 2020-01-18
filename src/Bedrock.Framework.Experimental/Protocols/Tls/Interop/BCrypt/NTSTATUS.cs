using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt
{
    internal enum NTSTATUS : uint
    {
        STATUS_SUCCESS = 0x0,
        STATUS_NOT_FOUND = 0xc0000225,
        STATUS_INVALID_PARAMETER = 0xc000000d,
        STATUS_NO_MEMORY = 0xc0000017,
        STATUS_INVALID_BUFFER_SIZE = 0xc0000206,
        STATUS_AUTH_TAG_MISMATCH = 0xC000A002,
    }
}
