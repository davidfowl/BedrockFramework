using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls
{
    public enum TlsProtocolVersion : ushort
    {
        Ssl3 = 0x0301,
        Tls11 = 0x0302,
        Tls12 = 0x0303,
        Tls13 = 0x0304,
        Tls13Draft18 = 0x7f12,
        Tls13Draft22 = 0x7f16,
        Tls13Draft23 = 0x7f17,
    }
}
