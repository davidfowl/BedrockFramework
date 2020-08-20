using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Bedrock.Framework.Protocols
{
    public class HubProtocol
    {
        private readonly Connection _connection;
        private readonly ProtocolReader _protocolReader;
        private readonly ProtocolWriter _protocolWriter;
        private readonly IMessageReader<HubMessage> _hubMessageReader;
        private readonly IMessageWriter<HubMessage> _hubMessageWriter;
        private readonly int? _maximumMessageSize;

        private HubProtocol(Connection connection, int? maximumMessageSize, IHubProtocol hubProtocol, IInvocationBinder invocationBinder)
        {
            _connection = connection;
            _protocolReader = connection.CreateReader();
            _protocolWriter = connection.CreateWriter();
            _hubMessageReader = new HubMessageReader(hubProtocol, invocationBinder);
            _hubMessageWriter = new HubMessageWriter(hubProtocol);
            _maximumMessageSize = maximumMessageSize;
        }

        public static HubProtocol CreateFromConnection(Connection connection, IHubProtocol hubProtocol, IInvocationBinder invocationBinder, int? maximumMessageSize = null)
        {
            return new HubProtocol(connection, maximumMessageSize, hubProtocol, invocationBinder);
        }

        public async ValueTask<HandshakeRequestMessage> ReadHandshakeAsync(CancellationToken cancellationToken = default)
        {
            var result = await _protocolReader.ReadAsync(new HubHandshakeMessageReader(), _maximumMessageSize, cancellationToken).ConfigureAwait(false);

            var message = result.Message;

            _protocolReader.Advance();

            return message;
        }

        public async ValueTask<HubMessage> ReadAsync(CancellationToken cancellationToken = default)
        {
            var result = await _protocolReader.ReadAsync(_hubMessageReader, cancellationToken).ConfigureAwait(false);

            var message = result.Message;

            _protocolReader.Advance();

            return message;
        }

        public ValueTask WriteAsync(HubMessage message, CancellationToken cancellationToken = default)
        {
            return _protocolWriter.WriteAsync(_hubMessageWriter, message, cancellationToken);
        }
    }
}
