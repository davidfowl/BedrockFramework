using Bedrock.Framework.Protocols;
using System.Net.Connections;
using System.Threading.Tasks;

namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ
{
    public class RabbitMQClientProtocol
    {
        private readonly ProtocolWriter _writer;
        private readonly ProtocolReader _reader;
        private readonly RabbitMQMessageFormatter _formatter;

        public RabbitMQClientProtocol(Connection connection)
        {             
            _writer = connection.CreateWriter();
            _reader = connection.CreateReader();
            _formatter = new RabbitMQMessageFormatter();
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
