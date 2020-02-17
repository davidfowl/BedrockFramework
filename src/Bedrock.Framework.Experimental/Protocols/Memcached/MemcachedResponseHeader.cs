using System;
using System.Collections.Generic;
using System.Text;
using static Bedrock.Framework.Experimental.Protocols.Memcached.Enums;

namespace Bedrock.Framework.Experimental.Protocols.Memcached
{
    public class MemcachedResponseHeader
    {
        public const byte Magic = 0x81;
        public Opcode Opcode { get; set; }
        public ushort KeyLength { get; set; }
        public byte ExtraLength { get; set; }
        public byte DataType { get; set; }
        public ResponseStatus ResponseStatus { get; set; }
        public uint TotalBodyLength { get; set; }
        public uint Opaque { get; set; }
        public ulong Cas { get; set; }
    }
}
