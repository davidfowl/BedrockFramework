using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods
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
        public ReadOnlyMemory<byte> Options { get; private set; }

        public QueueDeclare(ushort channel,ushort reserved1, string queueName, bool passive = false, bool durable = true, bool exclusive = false, bool autoDelete = false, bool noWait = false, Dictionary<string,object> arguments = null )
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
            Options = new ReadOnlyMemory<byte>(new byte[] { Convert.ToByte(passive), Convert.ToByte(durable), Convert.ToByte(exclusive), Convert.ToByte(autoDelete), Convert.ToByte(noWait) });
        }

        public bool TryParse(in ReadOnlySequence<byte> input, out SequencePosition end)
        {
            throw new NotImplementedException();
        }

        public void Write(IBufferWriter<byte> output)
        {          
            var payloadLength = 6 + 1 + QueueName.Length + sizeof(byte) + MethodHeaderLength;
            var buffer = output.GetSpan(RabbitMQMessageFormatter.HeaderLength + payloadLength + 1);
           
            WriteHeader(ref buffer, this.Channel, payloadLength);

            BinaryPrimitives.WriteUInt16BigEndian(buffer, ClassId);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(2), MethodId);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(4), Reserved1);
            buffer[6] = (byte)QueueName.Length;           
            Encoding.UTF8.GetBytes(QueueName).CopyTo(buffer.Slice(7));            
            var bytes = ProtocolHelper.BoolArrayToByte(Options);
            buffer = buffer.Slice(7 + QueueName.Length);
            buffer[0] = bytes;           
            //TO DO write table
            BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(1), 0);
            buffer[5] = (byte)FrameType.End;

            output.Advance(RabbitMQMessageFormatter.HeaderLength + payloadLength + sizeof(byte));
        }        
    }
}
