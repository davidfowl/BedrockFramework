using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Bedrock.Framework.Experimental.Protocols.Amqp
{
    public class AmqpClientProtocol
    {
        private readonly ProtocolWriter _writer;
        private readonly ProtocolReader _reader;
        private readonly AmqpMessageFormatter _formatter;

        public AmqpClientProtocol(ConnectionContext connection)
        {             
            _writer = connection.CreateWriter();
            _reader = connection.CreateReader();
            _formatter = new AmqpMessageFormatter();
        }

        public ValueTask SendAsync(IAmqpMessage message)
        {
            return _writer.WriteAsync(_formatter, message);
        }

        public async Task<T> ReceiveAsync<T>() where T : IAmqpMessage
        {
            var result = await _reader.ReadAsync(_formatter);
            _reader.Advance();
            return (T)result.Message;
        }        
    }
}
