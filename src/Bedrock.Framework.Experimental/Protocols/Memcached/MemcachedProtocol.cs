﻿using Bedrock.Framework.Experimental.Protocols.Memcached;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Bedrock.Framework.Experimental.Protocols.Memcached.Enums;

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

        private readonly SemaphoreSlim _semaphore;

        public MemcachedProtocol(ConnectionContext connection)
        {
            _connection = connection;

            _protocolReader = connection.CreateReader();
            _protocolWriter = connection.CreateWriter();

            _semaphore = new SemaphoreSlim(1);

            _memcachedMessageWriter = new MemcachedMessageWriter();
            _memcachedMessageReader = new MemcachedMessageReader();
        }

        private async Task<byte[]> CommandWithResult(MemcachedRequest request)
        {
            try
            {
                var result = await ExecuteCommand(request);
                if (result.Header.ResponseStatus == ResponseStatus.NoError)
                {
                    return result.Data.ToArray();
                }
                else
                {
                    throw new Exception(result.Header.ResponseStatus.ToString());
                }
            }
            finally
            {                
                _semaphore.Release();
            }
        }

        private async Task CommandWithNoResult(MemcachedRequest request)
        {
            try
            {
                var result = await ExecuteCommand(request);
                if (result.Header.ResponseStatus != ResponseStatus.NoError)
                {
                    throw new Exception(result.Header.ResponseStatus.ToString());
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
       
        public async Task<byte[]> Get(string key)
        {
            await _semaphore.WaitAsync();
            
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var request = new MemcachedRequest(Enums.Opcode.Get, keyBytes, NextOpaque);

            return await CommandWithResult(request);                   
        }

        public async Task Delete(string key)
        {
            await _semaphore.WaitAsync();
           
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var request = new MemcachedRequest(Enums.Opcode.Delete, keyBytes, NextOpaque);

            await CommandWithNoResult(request);
        }

        public async Task Set(string key, byte[] value, TimeSpan? expireIn)
        {
            await _semaphore.WaitAsync();

            var keyBytes = Encoding.UTF8.GetBytes(key);            
            var request = new MemcachedRequest(Enums.Opcode.Set, keyBytes, NextOpaque, value, TypeCode.Object, expireIn);

            await CommandWithNoResult(request);          
        }

        public async Task Add(string key, byte[] value, TimeSpan? expireIn)
        {
            await _semaphore.WaitAsync();

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var request = new MemcachedRequest(Enums.Opcode.Add, keyBytes, NextOpaque, value, TypeCode.Object, expireIn);

            await CommandWithNoResult(request);
        }

        public async Task Replace(string key, byte[] value, TimeSpan? expireIn)
        {
            await _semaphore.WaitAsync();

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var request = new MemcachedRequest(Enums.Opcode.Replace, keyBytes, NextOpaque, value, TypeCode.Object, expireIn);

            await CommandWithNoResult(request);
        }

        private async Task<MemcachedResponse> ExecuteCommand(MemcachedRequest request)
        {           
            await _protocolWriter.WriteAsync(_memcachedMessageWriter, request);
            var result = await _protocolReader.ReadAsync(_memcachedMessageReader);
            _protocolReader.Advance();
            return result.Message;
        }
    }
}
