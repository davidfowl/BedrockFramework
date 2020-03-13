using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ
{
    public interface IAmqpMessage
    {
        void Write(IBufferWriter<byte> output);
        bool TryParse(in ReadOnlySequence<byte> input, out SequencePosition end);
    }
}
