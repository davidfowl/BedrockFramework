using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp.Methods
{
    public class QueueDeclare : MethodBase, IAmqpMessage
    {
        public override byte ClassId => 50;
        public override byte MethodId => 10;

        public ushort Channel { get; private set; }
        public ushort Reserved1 { get; private set; }
        public string QueueName { get; private set; }
        public bool Passive { get; private set; }
        public bool Durable { get; private set; }
        public bool Exclusive { get; private set; }
        public bool AutoDelete { get; private set; }
        public bool NoWait { get; private set; }
        public Dictionary<string, object> Arguments { get; private set; }

        public QueueDeclare(ushort channel,ushort reserved1, string queueName, bool passive = false, bool durable = false, bool exclusive = false, bool autoDelete = false, bool noWait = false, Dictionary<string,object> arguments = null )
        {
            Channel = channel;
            Reserved1 = reserved1;
            QueueName = queueName;
            Passive = passive;
            Durable = durable;
            Exclusive = exclusive;
            AutoDelete = autoDelete;
            NoWait = noWait;
            Arguments = arguments;
        }
        public bool TryParse(ReadOnlySequence<byte> input, out SequencePosition end)
        {
            throw new NotImplementedException();
        }

        public void Write(IBufferWriter<byte> output)
        {          
            var payloadLength = 6 + 1 + QueueName.Length + sizeof(byte) + 4 ;
            var buffer = output.GetSpan(AmqpMessageFormatter.HeaderLength + payloadLength + 1);
           
            WriteHeader(ref buffer, this.Channel, payloadLength);
            BinaryPrimitives.WriteUInt16BigEndian(buffer, ClassId);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(2), MethodId);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(4), Reserved1);
            buffer[6] = (byte)QueueName.Length;
           
            Encoding.UTF8.GetBytes(QueueName).CopyTo(buffer.Slice(7));
            var bools = new bool[5] { Passive, Durable, Exclusive, AutoDelete, NoWait };
            var bytes = BoolArrayToByte(bools);
            buffer[7 + QueueName.Length] = bytes;
            buffer[7 + QueueName.Length+1] = 0;
            buffer[7 + QueueName.Length+2] = 0;
            buffer[7 + QueueName.Length+3] = 0;
            buffer[7 + QueueName.Length+4] = 0;
            //TO DO write table
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(7 + QueueName.Length + 5), 0);
            buffer[payloadLength] = (byte)FrameType.End;
            output.Advance(AmqpMessageFormatter.HeaderLength + payloadLength + sizeof(byte));
        }
        private byte BoolArrayToByte(bool[] source)
        {
            byte result = 0;           
            int index = 8 - source.Length;           
            foreach (bool b in source)
            {               
                if (b)
                    result |= (byte)(1 << (7 - index));
                index++;
            }
            return result;
        }
    }
}
