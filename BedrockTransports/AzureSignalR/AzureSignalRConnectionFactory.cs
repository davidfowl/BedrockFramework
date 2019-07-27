using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace BedrockTransports
{
    public class AzureSignalRConnectionFactory : IConnectionFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public AzureSignalRConnectionFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!(endpoint is AzureSignalREndPoint azEndpoint))
            {
                throw new NotSupportedException($"{endpoint} is not supported");
            }

            var options = new HttpConnectionOptions
            {
                Url = azEndpoint.Uri,
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true
            };
            options.Headers["Authorization"] = $"Bearer {azEndpoint.AccessToken}";
            var httpConnection = new HttpConnection(options, _loggerFactory);
            await httpConnection.StartAsync();

            await HandshakeAsync(httpConnection);

            return httpConnection;
        }


        private async Task HandshakeAsync(ConnectionContext connection)
        {
            var input = connection.Transport.Input;

            try
            {
                HandshakeProtocol.WriteRequestMessage(new HandshakeRequestMessage("json", 1), connection.Transport.Output);
                await connection.Transport.Output.FlushAsync();

                // cancellationToken already contains _state.StopCts.Token, so we don't have to link it again

                while (true)
                {
                    var result = await input.ReadAsync();

                    var buffer = result.Buffer;
                    var consumed = buffer.Start;
                    var examined = buffer.End;

                    try
                    {
                        // Read first message out of the incoming data
                        if (!buffer.IsEmpty)
                        {
                            if (HandshakeProtocol.TryParseResponseMessage(ref buffer, out var message))
                            {
                                // Adjust consumed and examined to point to the end of the handshake
                                // response, this handles the case where invocations are sent in the same payload
                                // as the negotiate response.
                                consumed = buffer.Start;
                                examined = consumed;

                                if (message.Error != null)
                                {
                                    // Log.HandshakeServerError(_logger, message.Error);
                                    throw new InvalidOperationException(
                                        $"Unable to complete handshake with the server due to an error: {message.Error}");
                                }

                                // Log.HandshakeComplete(_logger);
                                break;
                            }
                        }

                        if (result.IsCompleted)
                        {
                            // Not enough data, and we won't be getting any more data.
                            throw new InvalidOperationException(
                                "The server disconnected before sending a handshake response");
                        }
                    }
                    finally
                    {
                        input.AdvanceTo(consumed, examined);
                    }
                }
            }
            catch (InvalidDataException)
            {
                // Log.ErrorInvalidHandshakeResponse(_logger, ex);
                throw;
            }
            catch (OperationCanceledException)
            {
                //if (handshakeCts.IsCancellationRequested)
                //{
                //    // Log.ErrorHandshakeTimedOut(_logger, HandshakeTimeout, ex);
                //}
                //else
                //{
                //    // Log.ErrorHandshakeCanceled(_logger, ex);
                //}

                throw;
            }
            catch (Exception)
            {
                // Log.ErrorReceivingHandshakeResponse(_logger, ex);
                throw;
            }
        }
    }
}
