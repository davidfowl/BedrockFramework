using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    public struct WebSocketReadFrame
    {
        public WebSocketHeader Header { get; set; }

        public WebSocketPayloadReader Payload { get; set; }
    }
}
