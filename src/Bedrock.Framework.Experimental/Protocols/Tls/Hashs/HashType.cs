using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Hashs
{
    //Numbers from https://tools.ietf.org/html/rfc5246#section-7.4.1.4.1
    internal enum HashType : byte
    {
        SHA256 = 4,
        SHA384 = 5,
        SHA512 = 6,
    }
}
