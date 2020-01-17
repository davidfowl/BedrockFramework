using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.KeyExchanges
{
    //http://www.iana.org/assignments/tls-parameters/tls-parameters.xhtml
    //
    internal enum NamedGroup : ushort
    {
        None = 0,

        /* Elliptic Curve Groups (ECDHE) */
        secp256r1 = 0x0017,
        secp384r1 = 0x0018,
        secp521r1 = 0x0019,

        /* Elliptic curve functions */
        x25519 = 0x001D,
        x448 = 0x001E,

        /* Finite Field Groups (DHE) */
        ffdhe2048 = 0x0100,
        ffdhe3072 = 0x0101,
        ffdhe4096 = 0x0102,
        ffdhe6144 = 0x0103,
        ffdhe8192 = 0x0104,
    }
}
