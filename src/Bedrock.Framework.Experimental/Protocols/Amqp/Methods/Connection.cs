﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp.Methods
{
    public class ConnectionStart : MethodBase, IAmqpMessage
    {
        public override byte ClassId => 10;
        public override byte MethodId => 10;

        public byte VersionMajor { get; private set; }
        public byte VersionMinor { get; private set; }
        public Dictionary<string, object> ServerProperties { get; private set; }
        public string SecurityMechanims { get; private set; }
        public string Locale { get; private set; }

        public bool TryParse(ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            SequenceReader<byte> reader = new SequenceReader<byte>(input);

            reader.TryRead(out var verionMajor);
            reader.TryRead(out var versionMinor);

            this.VersionMajor = verionMajor;
            this.VersionMinor = versionMinor;
            try
            {
                ServerProperties = ProtocolHelper.ReadTable(ref reader);
               // SecurityMechanims = ProtocolHelper.ReadLongString(ref reader);
                SecurityMechanims = ProtocolHelper.ReadLongString(ref reader) switch
                {
                    "PLAIN AMQPLAIN" => "PLAIN",
                    _ => throw new Exception($"Unsupported security mechanism")
                };
                Locale = ProtocolHelper.ReadLongString(ref reader);

                end = reader.Position;
                return true;
            }
            catch(Exception ex)
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