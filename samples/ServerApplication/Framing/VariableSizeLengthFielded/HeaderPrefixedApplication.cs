using Bedrock.Framework.Experimental.Protocols.Framing.VariableSizeLengthFielded;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;

namespace ServerApplication.Framing.VariableSizeLengthFielded
{
    internal class HeaderPrefixedApplication : ConnectionHandler
    {
        private readonly ILogger _logger;
        private readonly HeaderFactory _headerFactory;

        public HeaderPrefixedApplication(ILogger<MyCustomProtocol> logger, HeaderFactory headerFactory)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _headerFactory = headerFactory ?? throw new System.ArgumentNullException(nameof(headerFactory));
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            _logger.LogInformation("{ConnectionId} connected.", connection.ConnectionId);

            // Use a header prefixed protocol
            var protocol = new VariableSizeLengthFieldedProtocol(Helper.HeaderLength, (headerSequence) => _headerFactory.CreateHeader(headerSequence));
            var reader = connection.CreateReader();
            var writer = connection.CreateWriter();

            while (true)
            {
                try
                {
                    var result = await reader.ReadAsync(protocol);
                    var message = result.Message;

                    _logger.LogInformation("Message received - Header: ({Header}) -  Payload: ({Payload})", message.Header, Encoding.UTF8.GetString(message.Payload.ToArray()));

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
                finally
                {
                    reader.Advance();
                }
            }

            _logger.LogInformation("{ConnectionId} disconnected.", connection.ConnectionId);
        }
    }
}
