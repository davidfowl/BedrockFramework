using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods
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

        private ReadOnlySpan<byte> PlainAMQPPlain => new byte[] { (byte)'P', (byte)'L', (byte)'A', (byte)'I', (byte)'N', (byte)' ', (byte)'A', (byte)'M', (byte)'Q', (byte)'P', (byte)'L', (byte)'A', (byte)'I', (byte)'N' };
        private ReadOnlyMemory<byte> Plain = new byte[] { (byte)'P', (byte)'L', (byte)'A', (byte)'I', (byte)'N' };
        public bool TryParse(ReadOnlySequence<byte> input,  out SequencePosition end)
        {
            SequenceReader<byte> reader = new SequenceReader<byte>(input);

            if (!reader.TryRead(out var verionMajor))
            {
                end = default;
                return false;
            }
            if (!reader.TryRead(out var versionMinor))
            {
                end = default;
                return false;
            }

            VersionMajor = verionMajor;
            VersionMinor = versionMinor;           
            try
            {
                ServerProperties = ProtocolHelper.ReadTable(ref reader);
                var security = ProtocolHelper.ReadLongString(ref reader);
                if(security.Span.SequenceEqual(PlainAMQPPlain))
                {
                    SecurityMechanims = Plain;
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
