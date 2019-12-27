﻿using System;
using System.Buffers;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Bedrock.Framework.Protocols
{
    public class HubHandshakeMessageReader : IMessageReader<HandshakeRequestMessage>
    {
        public bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out HandshakeRequestMessage message)
        {
            var buffer = input;
            if (HandshakeProtocol.TryParseRequestMessage(ref buffer, out message))
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
