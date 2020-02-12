using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using static Bedrock.Framework.Experimental.Protocols.Memcached.Enums;

namespace Bedrock.Framework.Experimental.Protocols.Memcached
{
    public class MemcachedMessageReader : IMessageReader<MemcachedResponse>
    { 
        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out MemcachedResponse message)
        {
            if (input.Length < Constants.HeaderLength)
            {
                message = default;
                return false;
            }
            message = new MemcachedResponse();
            if (input.First.Length >= Constants.HeaderLength)
            {
                message.ReadHeader(input.First.Span);
            }
            else
            {
                Span<byte> header = stackalloc byte[Constants.HeaderLength];
                input.Slice(0, Constants.HeaderLength).CopyTo(header);
                message.ReadHeader(header);
            }

            if (input.Length < message.Header.TotalBodyLength + Constants.HeaderLength)
            {
                message = default;
                return false;
            }

            message.ReadBody(input.Slice(Constants.HeaderLength, message.Header.TotalBodyLength));           
            consumed = input.Slice(Constants.HeaderLength + message.Header.TotalBodyLength).End;
            examined = consumed;          
            return true;
        }
    }
}
