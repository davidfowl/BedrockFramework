using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Bedrock.Framework.Protocols
{
    public class HubMessageWriter : IMessageWriter<HubMessage>
    {
        private readonly IHubProtocol _hubProtocol;

        public HubMessageWriter(IHubProtocol hubProtocol)
        {
            _hubProtocol = hubProtocol;
        }

        public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
        {
            _hubProtocol.WriteMessage(message, output);
        }
    }
}
