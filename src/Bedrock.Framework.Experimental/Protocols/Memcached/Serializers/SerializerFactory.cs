using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Memcached.Serializers
{
    public class SerializerFactory
    {
        public static ISerializer<T> GetSerializer<T>(TypeCode flags)
        {
            return flags switch
            {
                TypeCode.String => new StringSerializer() as ISerializer<T>,
                TypeCode.Object => new BinarySerializer<T>() as ISerializer<T>,
                _ => throw new ArgumentException($"flags {flags} not supported"),
            };
        }    
        
        public static ISerializer<T> GetSerializer<T>(out TypeCode flags)
        {
            flags = Type.GetTypeCode(typeof(T));
            return flags switch
            {
                TypeCode.String => new StringSerializer() as ISerializer<T>,
                TypeCode.Object => new BinarySerializer<T>() as ISerializer<T>,
                _ => throw new ArgumentException($"flags '{Type.GetTypeCode(typeof(T))}' is not supported")
            };
        }
    }
}
