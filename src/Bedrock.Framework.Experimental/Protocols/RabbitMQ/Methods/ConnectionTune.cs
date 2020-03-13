using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods
{
    public class ConnectionTune : MethodBase, IAmqpMessage
    { 
        public override byte ClassId => 10;
        public override byte MethodId => 30;

        public short MaxChannel { get; private set; }
        public int MaxFrame { get; private set; }
        public short HeartBeat { get; private set; }

        public bool TryParse(ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            SequenceReader<byte> reader = new SequenceReader<byte>(input);            
            try
            {
                reader.TryReadBigEndian(out short maxChannel);
                reader.TryReadBigEndian(out int maxFrame);
                reader.TryReadBigEndian(out short heartBeat);

                MaxChannel = maxChannel;
                MaxFrame = maxFrame;
                HeartBeat = heartBeat;

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
