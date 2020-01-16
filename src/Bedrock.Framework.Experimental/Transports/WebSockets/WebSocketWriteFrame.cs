using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    public struct WebSocketWriteFrame
    {
        public WebSocketHeader Header { get; set; }

        public ReadOnlySequence<byte> Payload { get; set; }
    }
}
