using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods
{
    public class QueueDeclareOk : MethodBase, IAmqpMessage
    { 
        public override byte ClassId => 50;
        public override byte MethodId => 11;
        public ReadOnlyMemory<byte> QueueName { get; private set; }
        public uint MessageCount { get; private set; }
        public uint ConsumerCount { get; private set; }

        public bool TryParse(in ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            var reader = new SequenceReader<byte>(input);            
            try
            {
                this.QueueName = ProtocolHelper.ReadShortString(ref reader);
                if (reader.TryReadBigEndian(out int messageCount))
                    this.MessageCount = (uint)messageCount;
                if (reader.TryReadBigEndian(out int consumerCount))
                    this.ConsumerCount = (uint)consumerCount;

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
