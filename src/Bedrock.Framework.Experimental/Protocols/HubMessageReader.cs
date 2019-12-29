using System;
using System.Buffers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Bedrock.Framework.Protocols
{
    public class HubMessageReader : IMessageReader<HubMessage>
    {
        private readonly IHubProtocol _hubProtocol;
        private readonly IInvocationBinder _invocationBinder;

        public HubMessageReader(IHubProtocol hubProtocol, IInvocationBinder invocationBinder)
        {
            _hubProtocol = hubProtocol;
            _invocationBinder = invocationBinder;
        }

        public bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out HubMessage message)
        {
            var buffer = input;
            if (_hubProtocol.TryParseMessage(ref buffer, _invocationBinder, out message))
            {
                consumed = buffer.End;
                examined = consumed;
                return true;
            }

            consumed = input.Start;
            examined = input.End;
            return false;
        }
    }
}
