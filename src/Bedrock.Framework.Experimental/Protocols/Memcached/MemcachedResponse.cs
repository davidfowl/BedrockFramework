using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using static Bedrock.Framework.Experimental.Protocols.Memcached.Enums;

namespace Bedrock.Framework.Experimental.Protocols.Memcached
{
    public class MemcachedResponse
    {
        public ReadOnlyMemory<byte> Data { get; private set; }
        public TypeCode Flags { get; set; }
        public MemcachedResponseHeader Header { get; private set; }

        public void ReadHeader(ReadOnlySpan<byte> buffer)
        {

            if (buffer[0] != MemcachedResponseHeader.Magic)
            {
                throw new ArgumentException("Magic mismatch");
            }

            this.Header = new MemcachedResponseHeader()
            {
                Opcode = (Opcode)buffer[1],
                KeyLength = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(2)),
                ExtraLength = buffer[4],
                DataType = buffer[5],
                ResponseStatus = (ResponseStatus)BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(6)),
                TotalBodyLength = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(8)),
                Opaque = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(12)),
                Cas = BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(16)),
            };
        }

        public void ReadBody(ReadOnlySequence<byte> sequence)
        {
            if (sequence.Length == 0)
            {
                return;
            }
                
            if (sequence.IsSingleSegment)
            {
                Flags = (TypeCode)BinaryPrimitives.ReadUInt32BigEndian(sequence.First.Span);
                Data = sequence.First.Slice(Header.KeyLength + Header.ExtraLength);
            }
            else
            {
                var buffer = MemoryPool<byte>.Shared.Rent((int)Header.TotalBodyLength).Memory.Slice(0, (int)Header.TotalBodyLength);
                int n = 0;
                foreach (var segment in sequence)
                {
                    segment.CopyTo(buffer.Slice(n));
                    n += segment.Length;
                }
                Flags = (TypeCode)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(0).Span);
                Data = buffer.Slice(Header.KeyLength + Header.ExtraLength);
            }
        }
    }
}
