using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Memcached
{
    public class MemcachedRequestHeader
    {
        public const byte Magic = 0x80;
        public ushort KeyLength { get; set; }
        public byte ExtraLength { get; set; }
        public byte DataType { get; set; }
        public ushort VBucket { get; set; }
        public uint TotalBodyLength { get; set; }
        public uint Opaque { get; set; }
        public ulong Cas { get; set; }
        public (TypeCode Flags, Expiration Expiration) Extras;
    }
}
