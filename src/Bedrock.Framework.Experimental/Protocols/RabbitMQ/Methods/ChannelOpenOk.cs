﻿using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods
{
    public class ChannelOpenOk : MethodBase, IAmqpMessage
    {
        public override byte ClassId => 20;
        public override byte MethodId => 11;       

        public ReadOnlyMemory<byte> Reserved1 { get; private set; }        

        public bool TryParse(in ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            var reader = new SequenceReader<byte>(input);
            try
            {
                Reserved1 = ProtocolHelper.ReadLongString(ref reader);
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
