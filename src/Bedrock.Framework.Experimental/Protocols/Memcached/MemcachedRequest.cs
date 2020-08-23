using System;
using System.Collections.Generic;
using System.Text;
using static Bedrock.Framework.Experimental.Protocols.Memcached.Enums;

namespace Bedrock.Framework.Experimental.Protocols.Memcached
{
    public class MemcachedRequest
    {
        public Opcode Opcode { get; }
        public byte[] Key { get; }
        public uint Opaque { get; }
        public byte[] Value { get; set; }
        public TypeCode Flags { get; }
        public TimeSpan? ExpireIn { get; }

        public MemcachedRequest(Opcode opcode, string key, uint opaque, byte[] value, TypeCode flags, TimeSpan? expireIn = null)
        {
            Opcode   = opcode;
            Key      = Encoding.UTF8.GetBytes(key);
            Opaque   = opaque;
            Value    = value;
            Flags    = flags;
            ExpireIn = expireIn;
        }

        public MemcachedRequest(Opcode opcode, byte[] key, uint opaque, byte[] value, TypeCode flags, TimeSpan? expireIn = null)
        {
            Opcode   = opcode;
            Key      = key;
            Opaque   = opaque;
            Value    = value;
            Flags    = flags;
            ExpireIn = expireIn;
        }

        public MemcachedRequest(Opcode opcode, byte[] key, uint opaque)
        {
            Opcode = opcode;
            Key    = key;
            Opaque = opaque;   
        }

        public MemcachedRequest(Opcode opcode, string key, uint opaque)
        {
            Opcode = opcode;
            Key    = Encoding.UTF8.GetBytes(key);
            Opaque = opaque;
        }
    }
}
