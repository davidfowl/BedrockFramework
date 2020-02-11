using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Memcached
{
    public class MemcachedMessageWriter : IMessageWriter<MemcachedRequest>
    {
        public void WriteMessage(MemcachedRequest message, IBufferWriter<byte> output)
        {           
            Span<byte> headerSpan = stackalloc byte[Constants.HeaderLength];
            var extraLength = 0;
            if (message.Opcode == Enums.Opcode.Set)
            {
                extraLength = 8;
            }
               
            var messageValue = 0;
            if (message.Value != null)
            {
                messageValue = message.Value.Length;
            }
               
            var header = new MemcachedRequestHeader()
            {                
                KeyLength = (ushort)message.Key.Length,
                Opaque = message.Opaque,
                TotalBodyLength = (uint)(extraLength + message.Key.Length + messageValue),
                ExtraLength = (byte)extraLength   
            };

            if (message.Opcode == Enums.Opcode.Set)
            {
                header.Extras = (message.Flags, message.ExpireIn);
            }
                
            headerSpan[0] = MemcachedRequestHeader.Magic;
            headerSpan[1] = (byte)message.Opcode;
            BinaryPrimitives.WriteUInt16BigEndian(headerSpan.Slice(2), header.KeyLength);
            headerSpan[4] = header.ExtraLength;
            headerSpan[5] = header.DataType;
            BinaryPrimitives.WriteUInt16BigEndian(headerSpan.Slice(6), header.VBucket);
            BinaryPrimitives.WriteUInt32BigEndian(headerSpan.Slice(8), header.TotalBodyLength);
            BinaryPrimitives.WriteUInt32BigEndian(headerSpan.Slice(12), header.Opaque);
            BinaryPrimitives.WriteUInt64BigEndian(headerSpan.Slice(16), header.Cas);

            output.Write(headerSpan);           
           
            var body = output.GetSpan((int)header.TotalBodyLength);            
            BinaryPrimitives.WriteUInt32BigEndian(body.Slice(0), (uint)header.Extras.Flags);
            BinaryPrimitives.WriteUInt32BigEndian(body.Slice(4), (uint)header.Extras.Expiration.Value);
            
            message.Key.CopyTo(body.Slice(header.ExtraLength));
            message.Value.CopyTo(body.Slice(header.ExtraLength + message.Key.Length));
            output.Advance((int)header.TotalBodyLength);    
        }
    }
}
