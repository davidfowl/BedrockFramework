using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls
{
    // From RFC 8446 Appendix B.1 https://tools.ietf.org/html/rfc8446#appendix-B.1
    internal enum TlsRecordType : byte
    {
        ChangeCipherSpec = 20,
        Alert = 21,
        Handshake = 22,
        AppData = 23,
        Invalid = 255,
        Incomplete = 0,
    }
}
