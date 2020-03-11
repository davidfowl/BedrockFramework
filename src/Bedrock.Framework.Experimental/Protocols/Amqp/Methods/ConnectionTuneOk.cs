using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp.Methods
{
    public class ConnectionTuneOk : MethodBase, IAmqpMessage
    {
        public override byte ClassId => 10;
        public override byte MethodId => 31;

        public short MaxChannel { get; private set; }
        public int MaxFrame { get; private set; }
        public short HeartBeat { get; private set; }

        public ConnectionTuneOk(short maxChannel, int maxFrame, short heartBeat)
        {
            this.MaxChannel = maxChannel;
            this.MaxFrame = maxFrame;
            this.HeartBeat = heartBeat;
        }

        public bool TryParse(ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            throw new NotImplementedException();
        }

        public void Write(IBufferWriter<byte> output)
        {  
            var payloadLength = sizeof(ushort) + sizeof(uint) + sizeof(ushort) + 4;
            var buffer = output.GetSpan(AmqpMessageFormatter.HeaderLength + payloadLength + 1);

            WriteHeader(ref buffer, 0, payloadLength);
            BinaryPrimitives.WriteUInt16BigEndian(buffer, ClassId);            
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(2), MethodId);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(4), (ushort)this.MaxChannel);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(6), (uint)this.MaxFrame);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(10), (ushort)this.HeartBeat);
            buffer[payloadLength]= (byte)FrameType.End;
            output.Advance(AmqpMessageFormatter.HeaderLength + payloadLength + sizeof(byte));
        }
    }
}
