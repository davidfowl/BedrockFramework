using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ
{
    public class RabbitMQProtocolVersionHeader : IAmqpMessage
    {
        private ReadOnlySpan<byte> ProtocolHeader => new byte[] { (byte)'A', (byte)'M', (byte)'Q', (byte)'P', 0, 0, 9, 1 };
        
        public bool TryParse(in ReadOnlySequence<byte> input, out SequencePosition end)
        {
            throw new NotImplementedException();
        }

        public void Write(IBufferWriter<byte> output)
        {
            var writer = new BufferWriter<IBufferWriter<byte>>(output);
            writer.Write(ProtocolHeader);
            writer.Commit();            
        }
    }
}
