using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp.Methods
{
    public class ConnectionOpenOk : MethodBase, IAmqpMessage
    { 
        public override byte ClassId => 10;
        public override byte MethodId => 41;

        public bool TryParse(ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            SequenceReader<byte> reader = new SequenceReader<byte>(input);            
            try
            {                

                end = reader.Position;
                return true;
            }
            catch (Exception ex)
            {
                //TODO trace error
                end = default;
                return false;
            }
        }

        public void Write(IBufferWriter<byte> output)
        {
            throw new NotImplementedException();
        }
    }
}
