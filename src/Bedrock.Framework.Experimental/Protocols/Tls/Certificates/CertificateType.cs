using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Certificates
{
    //https://www.iana.org/assignments/tls-parameters/tls-parameters.xhtml#tls-parameters-16
    //TLS SignatureAlgorithm Registry
    internal enum CertificateType
    {
        Anonymous = 0,
        Rsa = 1,
        Dsa = 2,
        Ecdsa = 3,
    }
}
