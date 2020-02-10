using Bedrock.Framework.Experimental.Protocols.Memcached;
using Bedrock.Framework.Experimental.Protocols.Memcached.Serializers;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Experimental.Protocols.Memcached
{
    public class MemcachedProtocol
    {        
        private readonly ConnectionContext _connection;
        private readonly MemcachedMessageWriter _memcachedMessageWriter;
        private readonly MemcachedMessageReader _memcachedMessageReader;
        private readonly ProtocolWriter _protocolWriter;
        private readonly ProtocolReader _protocolReader;

        private int _previousOpaque = 0;
        private uint NextOpaque => (uint)Interlocked.Increment(ref _previousOpaque);

        public MemcachedProtocol(ConnectionContext connection)
        {
            _connection = connection;
            _protocolReader = connection.CreateReader();
            _protocolWriter = connection.CreateWriter();
            _memcachedMessageWriter = new MemcachedMessageWriter();
            _memcachedMessageReader = new MemcachedMessageReader();
        }
        public async Task<T> Get<T>(string key)
        {   
            var keyBytes = Encoding.UTF8.GetBytes(key);           
            var request = new MemcachedRequest(Enums.Opcode.Get, keyBytes, NextOpaque);
            await _protocolWriter.WriteAsync(_memcachedMessageWriter, request);
            var result = await _protocolReader.ReadAsync(_memcachedMessageReader);
            ISerializer<T> serializer = SerializerFactory.GetSerializer<T>(result.Message.Flags);
            _protocolReader.Advance();
            return (T)serializer.Deserialize(result.Message.Data);
        }
        public async Task Set<T>(string key, T value, TimeSpan? expireIn)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);            
            ISerializer<T> serializer = SerializerFactory.GetSerializer<T>(out TypeCode flags);
            var request = new MemcachedRequest(Enums.Opcode.Set, keyBytes, NextOpaque, serializer.Serialize(value), flags, expireIn);
            await _protocolWriter.WriteAsync(_memcachedMessageWriter, request);
            var result = await _protocolReader.ReadAsync(_memcachedMessageReader);
            _protocolReader.Advance();
            
        }
    }
}
