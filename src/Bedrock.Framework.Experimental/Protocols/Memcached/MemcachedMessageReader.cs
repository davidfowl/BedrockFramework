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
        public MemcachedResponse InProgressResponse { get; private set; } = new MemcachedResponse();
      

        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out MemcachedResponse message)
        {
            if (input.Length < Constants.HeaderLength)
            {
                message = default;
                return false;
            }
            if (input.First.Length >= Constants.HeaderLength)
            {
                InProgressResponse.ReadHeader(input.First.Span);
            }
            else
            {
                Span<byte> header = stackalloc byte[Constants.HeaderLength];
                input.Slice(0, Constants.HeaderLength).CopyTo(header);
                InProgressResponse.ReadHeader(header);
            }
            if (input.Length < InProgressResponse.Header.TotalBodyLength + Constants.HeaderLength)
            {
                message = default;
                return false;
            }

            InProgressResponse.ReadBody(input.Slice(Constants.HeaderLength, InProgressResponse.Header.TotalBodyLength));
            var a = input.Slice(Constants.HeaderLength + InProgressResponse.Header.TotalBodyLength);
            consumed = a.End;
            examined = consumed;
            message = InProgressResponse;
            InProgressResponse = new MemcachedResponse();
            return true;

            
        }
    }
}
