using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Certificates
{
    //https://www.iana.org/assignments/tls-parameters/tls-parameters.xhtml#tls-parameters-16
    //TLS SignatureAlgorithm Registry
    internal enum CertificateType
    {
        anonymous = 0,
        rsa = 1,
        dsa = 2,
        ecdsa = 3,
    }
}
