using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Certificates
{
    internal enum SignatureScheme : ushort
    {
        none = 0,
        /* RSASSA-PKCS1-v1_5 algorithms */
        rsa_pkcs1_sha1 = 0x0201,
        rsa_pkcs1_sha256 = 0x0401,
        rsa_pkcs1_sha384 = 0x0501,
        rsa_pkcs1_sha512 = 0x0601,

        /* ECDSA algorithms */
        ecdsa_secp256r1_sha256 = 0x0403,
        ecdsa_secp384r1_sha384 = 0x0503,
        ecdsa_secp521r1_sha512 = 0x0603,

        /* RSASSA-PSS algorithms */
        rsa_pss_sha256 = 0x0804,
        rsa_pss_sha384 = 0x0805,
        rsa_pss_sha512 = 0x0806,

        /* EdDSA algorithms */
        ed25519 = 0x0807,
        ed448 = 0x0808,
    }
}
