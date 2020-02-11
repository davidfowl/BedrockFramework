using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Memcached.Serializers
{
    public interface ISerializer<T>
    {
        byte[] Serialize(T src);
        T Deserialize(ReadOnlyMemory<byte> src);
    }

    public class StringSerializer : ISerializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> src) => Encoding.UTF8.GetString(src.Span);
        public byte[] Serialize(string src) => Encoding.UTF8.GetBytes(src);
    }

    public class BinarySerializer<T> : ISerializer<T>
    {
        public T Deserialize(ReadOnlyMemory<byte> src)
        {
            using var ms = new MemoryStream(src.ToArray());
            var formatter = new BinaryFormatter();
            return (T)formatter.Deserialize(ms);
        }

        public byte[] Serialize(T src)
        {
            using var stream = new MemoryStream();
            var formatter = new BinaryFormatter();
            formatter.Serialize(stream, src);
            return stream.ToArray();
        }
    }
}
