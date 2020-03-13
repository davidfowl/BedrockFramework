using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods
{
    public class QueueDelete : MethodBase, IAmqpMessage
    {
        public override byte ClassId => 50;
        public override byte MethodId => 40;

        public ushort Channel { get; private set; }
        public ushort Reserved1 { get; private set; }
        public string QueueName { get; private set; }
        public bool DeleteIfUnused { get; private set; }
        public bool DeleteIfEmpty { get; private set; }       
                
        public ReadOnlyMemory<byte> Options { get; private set; }

        public QueueDelete(ushort channel,ushort reserved1, string queueName, bool deleteIfUnused = false, bool deleteIfEmpty = false )
        {
            Channel = channel;
            Reserved1 = reserved1;
            QueueName = queueName;
           
            Options = new ReadOnlyMemory<byte>(new byte[] { Convert.ToByte(deleteIfUnused), Convert.ToByte(deleteIfEmpty) });
        }

        public bool TryParse(in ReadOnlySequence<byte> input, out SequencePosition end)
        {
            throw new NotImplementedException();
        }

        public void Write(IBufferWriter<byte> output)
        {          
            var payloadLength = MethodHeaderLength + 2 + 1 + QueueName.Length + sizeof(byte);
            var buffer = output.GetSpan(RabbitMQMessageFormatter.HeaderLength + payloadLength + 1);
           
            WriteHeader(ref buffer, this.Channel, payloadLength);

            BinaryPrimitives.WriteUInt16BigEndian(buffer, ClassId);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(2), MethodId);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.Slice(4), Reserved1);
            buffer[6] = (byte)QueueName.Length;           
            Encoding.UTF8.GetBytes(QueueName).CopyTo(buffer.Slice(7)); 
            buffer[7 + QueueName.Length] = ProtocolHelper.BoolArrayToByte(Options); 
            buffer[payloadLength] = (byte)FrameType.End;

            output.Advance(RabbitMQMessageFormatter.HeaderLength + payloadLength + sizeof(byte));
        }        
    }
}
