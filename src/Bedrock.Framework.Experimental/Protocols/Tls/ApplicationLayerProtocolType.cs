using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls
{
    internal enum ApplicationLayerProtocolType
    {
        None = -1,
        Http1_1 = 0,
        Spdy1 = 1,
        Spdy2 = 2,
        Spdy3 = 3,
        Turn = 4,
        Stun = 5,
        Http2_Tls = 6,
        Http2_Tcp = 7,
        WebRtc = 8,
        Confidential_WebRtc = 9,
        Ftp = 10
    }
}
