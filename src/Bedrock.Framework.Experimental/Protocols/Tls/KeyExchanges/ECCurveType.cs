using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.KeyExchanges
{
    //http://www.iana.org/assignments/tls-parameters/tls-parameters.xhtml
    internal enum ECCurveType : byte
    {
        explicit_prime = 1,
        explicit_char2 = 2,
        named_curve = 3,
    }
}
