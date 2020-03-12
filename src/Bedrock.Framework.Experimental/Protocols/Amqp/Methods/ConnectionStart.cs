using System;
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
        public ReadOnlyMemory<byte> SecurityMechanims { get; private set; }
        public ReadOnlyMemory<byte> Locale { get; private set; }

        public bool TryParse(ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            SequenceReader<byte> reader = new SequenceReader<byte>(input);

            reader.TryRead(out var verionMajor);
            reader.TryRead(out var versionMinor);

            VersionMajor = verionMajor;
            VersionMinor = versionMinor;           
            try
            {
                ServerProperties = ProtocolHelper.ReadTable(ref reader);
                var security = ProtocolHelper.ReadLongString(ref reader);
                if(security.Span.IndexOf(Encoding.UTF8.GetBytes("PLAIN AMQPLAIN").AsSpan()) >= 0)
                {
                    SecurityMechanims = Encoding.UTF8.GetBytes("PLAIN").AsMemory();
                }
                else
                {
                    throw new Exception($"Unsupported security mechanism");
                }                
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
