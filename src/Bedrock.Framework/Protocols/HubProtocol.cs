using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Bedrock.Framework.Protocols
{
    public class HubProtocol
    {
        private readonly ConnectionContext _connection;
        private readonly ProtocolReader<HandshakeRequestMessage> _handshakeReader;
        private readonly ProtocolReader<HubMessage> _hubProtocolReader;
        private readonly ProtocolWriter<HubMessage> _hubProtocolWriter;

        private HubProtocol(ConnectionContext connection, int? maximumMessageSize, IHubProtocol hubProtocol, IInvocationBinder invocationBinder)
        {
            _connection = connection;
            _handshakeReader = connection.CreateReader(new HubHandshakeProtocolReader(), maximumMessageSize);
            _hubProtocolReader = connection.CreateReader(new HubProtocolReader(hubProtocol, invocationBinder), maximumMessageSize);
            _hubProtocolWriter = connection.CreateWriter(new HubProtocolWriter(hubProtocol));
        }

        public static HubProtocol CreateFromConnection(ConnectionContext connection, IHubProtocol hubProtocol, IInvocationBinder invocationBinder, int? maximumMessageSize = null)
        {
            return new HubProtocol(connection, maximumMessageSize, hubProtocol, invocationBinder);
        }

        public async ValueTask<HandshakeRequestMessage> ReadHandshakeAsync(CancellationToken cancellationToken = default)
        {
            var result = await _handshakeReader.ReadAsync(cancellationToken);

            var message = result.Message;

            _handshakeReader.Advance();

            return message;
        }

        public async ValueTask<HubMessage> ReadAsync(CancellationToken cancellationToken = default)
        {
            var result = await _hubProtocolReader.ReadAsync(cancellationToken);

            var message = result.Message;

            _hubProtocolReader.Advance();

            return message;
        }

        public ValueTask WriteAsync(HubMessage message, CancellationToken cancellationToken = default)
        {
            return _hubProtocolWriter.WriteAsync(message, cancellationToken);
        }
    }
}
