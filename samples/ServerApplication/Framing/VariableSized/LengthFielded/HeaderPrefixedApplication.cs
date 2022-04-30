using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework.Experimental.Protocols.Framing.VariableSized.LengthFielded;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace ServerApplication.Framing.VariableSized.LengthFielded
{
    internal class HeaderPrefixedApplication : ConnectionHandler
    {
        private readonly ILogger _logger;
        private readonly HeaderFactory _headerFactory;

        public HeaderPrefixedApplication(ILogger<HeaderPrefixedApplication> logger, HeaderFactory headerFactory)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _headerFactory = headerFactory ?? throw new System.ArgumentNullException(nameof(headerFactory));
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            _logger.LogInformation("{ConnectionId} connected.", connection.ConnectionId);

            // Use the header prefixed protocol
            var headerFactory = _headerFactory; // Capturing members in anonymous methods results memory leak, that's why we introduce a local variable.
            var protocol = new LengthFieldedProtocol(Helper.HeaderLength, (headerSequence) => headerFactory.CreateHeader(headerSequence));
            var reader = connection.CreateReader();
            var writer = connection.CreateWriter();

            while (true)
            {
                try
                {
                    var result = await reader.ReadAsync(protocol);
                    var message = result.Message;

#if NETCOREAPP3_1
                    _logger.LogInformation("Message received - Header: ({Header}) -  Payload: ({Payload})", message.Header, Encoding.UTF8.GetString(message.Payload.ToArray()));
#elif NET6_0_OR_GREATER
                    _logger.LogInformation("Message received - Header: ({Header}) -  Payload: ({Payload})", message.Header, Encoding.UTF8.GetString(message.Payload));
#endif
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
