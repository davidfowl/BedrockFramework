using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Bedrock.Framework.Protocols;

namespace Bedrock.Framework.Protocols
{
    public interface IHttpBodyReader : IMessageReader<ReadOnlySequence<byte>>
    {
        bool IsCompleted { get; }
    }
}
