using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp
{
    public class AmqpProtocolversionHeader : IAmqpMessage
    {
        public bool TryParse(ReadOnlySequence<byte> input, out SequencePosition end)
        {
            throw new NotImplementedException();
        }

        public void Write(IBufferWriter<byte> output)
        {
            var headerSpan = output.GetSpan(8);
            headerSpan[0] = (byte)'A';
            headerSpan[1] = (byte)'M';
            headerSpan[2] = (byte)'Q';
            headerSpan[3] = (byte)'P';
            headerSpan[4] = (byte)0;
            headerSpan[5] = (byte)0;
            headerSpan[6] = (byte)9;
            headerSpan[7] = (byte)1;           
            output.Advance(8);
        }
    }
}
