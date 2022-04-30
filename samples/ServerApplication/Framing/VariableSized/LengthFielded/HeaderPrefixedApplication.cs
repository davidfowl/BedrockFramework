using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework.Experimental.Protocols.Framing.VariableSized.LengthFielded;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace ServerApplication.Framing.VariableSized.LengthFielded
{
    internal partial class HeaderPrefixedApplication : ConnectionHandler
    {
        private readonly ILogger _logger;
        private readonly HeaderFactory _headerFactory;

        #region Logs
#if NETCOREAPP3_1
        private static readonly Action<ILogger, string, Exception?> _logConnected =
            LoggerMessage.Define<string>(logLevel: LogLevel.Information, eventId: 0, formatString: "{ConnectionId} connected.");

        private static readonly Action<ILogger, IHeader, string, Exception?> _logMessageReceived =
            LoggerMessage.Define<IHeader, string>(logLevel: LogLevel.Information, eventId: 0, formatString: "Message received - Header: ({Header}) - Payload: ({Payload})");

        private static readonly Action<ILogger, string, Exception?> _logDisconnected =
            LoggerMessage.Define<string>(logLevel: LogLevel.Information, eventId: 0, formatString: "{ConnectionId} disconnected.");
#elif NET6_0_OR_GREATER
        [LoggerMessage(0, LogLevel.Information, "{ConnectionId} connected.")]
        partial void LogConnected(string connectionId);

        [LoggerMessage(0, LogLevel.Information, "Message received - Header: ({Header}) - Payload: ({Payload})")]
        partial void LogMessageReceived(IHeader header, string payload);

        [LoggerMessage(0, LogLevel.Information, "{ConnectionId} disconnected.")]
        partial void LogDisconnected(string connectionId);
#endif
        #endregion

        public HeaderPrefixedApplication(ILogger<HeaderPrefixedApplication> logger, HeaderFactory headerFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _headerFactory = headerFactory ?? throw new ArgumentNullException(nameof(headerFactory));
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
#if NETCOREAPP3_1
            _logConnected(_logger, connection.ConnectionId, null);
#elif NET6_0_OR_GREATER
            LogConnected(connection.ConnectionId);
#endif
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
                    _logMessageReceived(_logger, message.Header, Encoding.UTF8.GetString(message.Payload.ToArray()), null);
#elif NET6_0_OR_GREATER
                    LogMessageReceived(message.Header, Encoding.UTF8.GetString(message.Payload));
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

#if NETCOREAPP3_1
            _logDisconnected(_logger, connection.ConnectionId, null);
#elif NET6_0_OR_GREATER
            LogDisconnected(connection.ConnectionId);
#endif
        }
    }
}
