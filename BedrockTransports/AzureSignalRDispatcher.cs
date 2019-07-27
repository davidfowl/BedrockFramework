using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace BedrockTransports
{
    internal abstract class AzureSignalRDispatcher
    {
        private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(15);
        // Service ping rate is 5 sec to let server know service status. Set timeout for 30 sec for some space.
        private static readonly TimeSpan DefaultServiceTimeout = TimeSpan.FromSeconds(30);
        private static readonly long DefaultServiceTimeoutTicks = DefaultServiceTimeout.Seconds * Stopwatch.Frequency;
        // App server ping rate is 5 sec to let service know if app server is still alive
        // Service will abort both server and client connections link to this server when server is down.
        // App server ping is triggered by incoming requests and send by checking last send timestamp.
        private static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(5);
        private static readonly long DefaultKeepAliveTicks = DefaultKeepAliveInterval.Seconds * Stopwatch.Frequency;

        private readonly ServiceProtocol ServiceProtocol = new ServiceProtocol();
        private readonly HandshakeRequestMessage _handshakeRequest;
        private readonly ReadOnlyMemory<byte> _cachedPingBytes;
        private readonly SemaphoreSlim _serviceConnectionLock = new SemaphoreSlim(1, 1);

        // Check service timeout
        private long _lastReceiveTimestamp;

        // Keep-alive tick
        private long _lastSendTimestamp;

        private Task _processingTask = Task.CompletedTask;
        private readonly Uri _uri;
        private readonly string _token;
        private ILoggerFactory _loggerFactory;

        public AzureSignalRDispatcher(Uri uri, string token, ILoggerFactory loggerFactory)
        {
            _uri = uri;
            _token = token;
            _loggerFactory = loggerFactory;
            Logger = loggerFactory.CreateLogger<AzureSignalRDispatcher>();
            _cachedPingBytes = ServiceProtocol.GetMessageBytes(PingMessage.Instance);
            _handshakeRequest = new HandshakeRequestMessage(ServiceProtocol.Version);
        }

        protected string ConnectionId { get; } = Guid.NewGuid().ToString();

        public string ErrorMessage { get; set; }

        public ILogger Logger { get; set; }

        public EndPoint EndPoint { get; set; }

        private ConnectionContext ConnectionContext { get; set; }

        /// <summary>
        /// Start a service connection without the lifetime management.
        /// To get full lifetime management including dispose or restart, use <see cref="ServiceConnectionContainerBase"/>
        /// </summary>
        /// <param name="target">The target instance Id</param>
        /// <returns>The task of StartAsync</returns>
        public async Task StartAsync()
        {
            // Codes in try block should catch and log exceptions separately.
            // The catch here should be very rare to reach.
            try
            {
                if (await StartAsyncCore())
                {
                    _processingTask = ProcessIncomingAsync();
                }
                else
                {
                    await StopAsync();
                }
            }
            catch (Exception ex)
            {
                // Status = ServiceConnectionStatus.Disconnected;
                Log.UnexpectedExceptionInStart(Logger, ConnectionId, ex);

                await StopAsync();

                throw;
            }
        }

        public Task StopAsync()
        {
            try
            {
                ConnectionContext?.Transport.Input.CancelPendingRead();
            }
            catch (Exception ex)
            {
                Log.UnexpectedExceptionInStop(Logger, ConnectionId, ex);
            }

            return (_processingTask ?? Task.CompletedTask);
        }

        public virtual async Task WriteAsync(ServiceMessage serviceMessage)
        {
            // We have to lock around outgoing sends since the pipe is single writer.
            // The lock is per serviceConnection
            await _serviceConnectionLock.WaitAsync();

            var errorMessage = ErrorMessage;
            if (!string.IsNullOrEmpty(errorMessage))
            {
                _serviceConnectionLock.Release();
                throw new InvalidOperationException(errorMessage);
            }

            if (ConnectionContext == null)
            {
                _serviceConnectionLock.Release();
                throw new InvalidOperationException();
            }

            try
            {
                // Write the service protocol message
                ServiceProtocol.WriteMessage(serviceMessage, ConnectionContext.Transport.Output);
                await ConnectionContext.Transport.Output.FlushAsync();
            }
            catch (Exception ex)
            {
                Log.FailedToWrite(Logger, ConnectionId, ex);
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        protected async Task<ConnectionContext> CreateConnectionAsync()
        {
            var options = new HttpConnectionOptions
            {
                Url = _uri,
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true
            };
            options.Headers["Authorization"] = $"Bearer {_token}";
            var httpConnection = new HttpConnection(options, _loggerFactory);
            await httpConnection.StartAsync();
            return httpConnection;
        }

        protected Task DisposeConnectionAsync()
        {
            return ConnectionContext.DisposeAsync().AsTask();
        }

        protected abstract Task CleanupConnectionsAsync();

        protected abstract Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage);

        protected abstract Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage);

        protected abstract Task OnMessageAsync(ConnectionDataMessage connectionDataMessage);

        protected Task OnServiceErrorAsync(ServiceErrorMessage serviceErrorMessage)
        {
            if (!string.IsNullOrEmpty(serviceErrorMessage.ErrorMessage))
            {
                // When receives service error message, we suppose server -> service connection doesn't work,
                // and set ErrorMessage to prevent sending message from server to service
                // But messages in the pipe from service -> server should be processed as usual. Just log without
                // throw exception here.
                // ErrorMessage = serviceErrorMessage.ErrorMessage;
                Log.ReceivedServiceErrorMessage(Logger, ConnectionId, serviceErrorMessage.ErrorMessage);
            }

            return Task.CompletedTask;
        }

        protected Task OnPingMessageAsync(PingMessage pingMessage)
        {
            return Task.CompletedTask;
        }

        protected Task OnAckMessageAsync(AckMessage ackMessage)
        {
            return Task.CompletedTask;
        }

        private async Task<bool> StartAsyncCore()
        {
            // Lock here in case somebody tries to send before the connection is assigned
            await _serviceConnectionLock.WaitAsync();

            try
            {
                // Status = ServiceConnectionStatus.Connecting;
                ConnectionContext = await CreateConnectionAsync();
                ErrorMessage = null;

                if (await HandshakeAsync())
                {
                    Log.ServiceConnectionConnected(Logger, ConnectionId);
                    // Status = ServiceConnectionStatus.Connected;
                    return true;
                }
                else
                {
                    // False means we got a HandshakeResponseMessage with error. Will take below actions:
                    // - Dispose the connection
                    // Status = ServiceConnectionStatus.Disconnected;
                    await DisposeConnectionAsync();

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.FailedToConnect(Logger, "HubEndpoint.ToString()", ConnectionId, ex);

                // Status = ServiceConnectionStatus.Disconnected;
                await DisposeConnectionAsync();

                return false;
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        protected virtual async Task<bool> HandshakeAsync()
        {
            await SendHandshakeRequestAsync(ConnectionContext.Transport.Output);

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    if (!Debugger.IsAttached)
                    {
                        cts.CancelAfter(DefaultHandshakeTimeout);
                    }

                    if (await ReceiveHandshakeResponseAsync(ConnectionContext.Transport.Input, cts.Token))
                    {
                        Log.HandshakeComplete(Logger);
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.ErrorReceivingHandshakeResponse(Logger, ConnectionId, ex);
                throw;
            }
        }

        private async Task SendHandshakeRequestAsync(PipeWriter output)
        {
            Log.SendingHandshakeRequest(Logger);

            ServiceProtocol.WriteMessage(_handshakeRequest, output);
            var sendHandshakeResult = await output.FlushAsync();
            if (sendHandshakeResult.IsCompleted)
            {
                throw new InvalidOperationException("Service disconnected before handshake complete.");
            }
        }

        private async Task<bool> ReceiveHandshakeResponseAsync(PipeReader input, CancellationToken token)
        {
            while (true)
            {
                var result = await input.ReadAsync(token);

                var buffer = result.Buffer;
                var consumed = buffer.Start;
                var examined = buffer.End;

                try
                {
                    if (result.IsCanceled)
                    {
                        throw new InvalidOperationException("Connection cancelled before handshake complete.");
                    }

                    if (!buffer.IsEmpty)
                    {
                        if (ServiceProtocol.TryParseMessage(ref buffer, out var message))
                        {
                            consumed = buffer.Start;
                            examined = consumed;

                            if (!(message is HandshakeResponseMessage handshakeResponse))
                            {
                                throw new InvalidDataException(
                                    $"{message.GetType().Name} received when waiting for handshake response.");
                            }

                            if (string.IsNullOrEmpty(handshakeResponse.ErrorMessage))
                            {
                                return true;
                            }

                            // Handshake error. Will stop reconnect.
                            //if (_connectionType == ServerConnectionType.OnDemand)
                            //{
                            //    // Handshake errors on on-demand connections are acceptable.
                            //    Log.OnDemandConnectionHandshakeResponse(Logger, handshakeResponse.ErrorMessage);
                            //}
                            //else
                            //{
                            //    Log.HandshakeError(Logger, handshakeResponse.ErrorMessage, ConnectionId);
                            //}
                            Log.HandshakeError(Logger, handshakeResponse.ErrorMessage, ConnectionId);

                            return false;
                        }
                    }

                    if (result.IsCompleted)
                    {
                        // Not enough data, and we won't be getting any more data.
                        throw new InvalidOperationException("Service disconnected before sending a handshake response.");
                    }
                }
                finally
                {
                    input.AdvanceTo(consumed, examined);
                }
            }
        }

        private async Task ProcessIncomingAsync()
        {
            var keepAliveTimer = StartKeepAliveTimer();
            try
            {
                while (true)
                {
                    var result = await ConnectionContext.Transport.Input.ReadAsync();
                    var buffer = result.Buffer;

                    try
                    {
                        if (result.IsCanceled)
                        {
                            Log.ReadingCancelled(Logger, ConnectionId);
                            break;
                        }

                        if (!buffer.IsEmpty)
                        {
                            Log.ReceivedMessage(Logger, buffer.Length, ConnectionId);

                            UpdateReceiveTimestamp();

                            // No matter what kind of message come in, trigger send ping check
                            _ = TrySendPingAsync();

                            while (ServiceProtocol.TryParseMessage(ref buffer, out var message))
                            {
                                _ = DispatchMessageAsync(message);
                            }
                        }

                        if (result.IsCompleted)
                        {
                            // The connection is closed (reconnect)
                            Log.ServiceConnectionClosed(Logger, ConnectionId);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Error occurs in handling the message, but the connection between SDK and service still works.
                        // So, just log error instead of breaking the connection
                        Log.ErrorProcessingMessages(Logger, ConnectionId, ex);
                    }
                    finally
                    {
                        ConnectionContext.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            catch (Exception ex)
            {
                // Fatal error: There is something wrong for the connection between SDK and service.
                // Abort all the client connections, close the httpConnection.
                // Only reconnect can recover.
                Log.ConnectionDropped(Logger, "HubEndpoint.ToString()", ConnectionId, ex);
            }
            finally
            {
                keepAliveTimer.Stop();

                await _serviceConnectionLock.WaitAsync();
                try
                {
                    // Status = ServiceConnectionStatus.Disconnected;
                    await DisposeConnectionAsync();
                }
                finally
                {
                    _serviceConnectionLock.Release();
                }
            }

            // TODO: Never cleanup connections unless Service asks us to do that
            // Current implementation is based on assumption that Service will drop clients
            // if server connection fails.
            await CleanupConnectionsAsync();
        }

        private Task DispatchMessageAsync(ServiceMessage message)
        {
            switch (message)
            {
                case OpenConnectionMessage openConnectionMessage:
                    return OnConnectedAsync(openConnectionMessage);
                case CloseConnectionMessage closeConnectionMessage:
                    return OnDisconnectedAsync(closeConnectionMessage);
                case ConnectionDataMessage connectionDataMessage:
                    return OnMessageAsync(connectionDataMessage);
                case ServiceErrorMessage serviceErrorMessage:
                    return OnServiceErrorAsync(serviceErrorMessage);
                case PingMessage pingMessage:
                    return OnPingMessageAsync(pingMessage);
                case AckMessage ackMessage:
                    return OnAckMessageAsync(ackMessage);
            }
            return Task.CompletedTask;
        }

        private TimerAwaitable StartKeepAliveTimer()
        {
            Log.StartingKeepAliveTimer(Logger, DefaultKeepAliveInterval);

            _lastReceiveTimestamp = Stopwatch.GetTimestamp();
            _lastSendTimestamp = _lastReceiveTimestamp;
            var timer = new TimerAwaitable(DefaultKeepAliveInterval, DefaultKeepAliveInterval);
            _ = KeepAliveAsync(timer);

            return timer;
        }

        private void UpdateReceiveTimestamp()
        {
            Interlocked.Exchange(ref _lastReceiveTimestamp, Stopwatch.GetTimestamp());
        }

        private async Task KeepAliveAsync(TimerAwaitable timer)
        {
            using (timer)
            {
                timer.Start();

                while (await timer)
                {
                    if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastReceiveTimestamp) > DefaultServiceTimeoutTicks)
                    {
                        AbortConnection();
                        // We shouldn't get here twice.
                        continue;
                    }
                }
            }
        }

        private async ValueTask TrySendPingAsync()
        {
            if (!_serviceConnectionLock.Wait(0))
            {
                // Skip sending PingMessage when failed getting lock
                return;
            }

            try
            {
                // Check if last send time is longer than default keep-alive ticks and then send ping
                if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastSendTimestamp) > DefaultKeepAliveTicks)
                {
                    await ConnectionContext.Transport.Output.WriteAsync(GetPingMessage());
                    Interlocked.Exchange(ref _lastSendTimestamp, Stopwatch.GetTimestamp());
                    Log.SentPing(Logger);
                }
            }
            catch (Exception ex)
            {
                Log.FailedSendingPing(Logger, ConnectionId, ex);
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        protected virtual ReadOnlyMemory<byte> GetPingMessage() => _cachedPingBytes;

        private void AbortConnection()
        {
            if (!_serviceConnectionLock.Wait(0))
            {
                // Couldn't get the lock so skip the cancellation (we could be in the middle of reconnecting?)
                return;
            }

            try
            {
                // Stop the reading from connection
                if (ConnectionContext != null)
                {
                    ConnectionContext.Transport.Input.CancelPendingRead();
                    Log.ServiceTimeout(Logger, DefaultServiceTimeout, ConnectionId);
                }
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        private static class Log
        {
            // Category: ServiceConnection
            private static readonly Action<ILogger, string, string, Exception> _failedToWrite =
                LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1, "FailedToWrite"), "Failed to send message to the service: {message}. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, string, string, string, Exception> _failedToConnect =
                LoggerMessage.Define<string, string, string>(LogLevel.Error, new EventId(2, "FailedToConnect"), "Failed to connect to '{endpoint}', will retry after the back off period. Error detail: {message}. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, string, Exception> _errorProcessingMessages =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "ErrorProcessingMessages"), "Error when processing messages. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, string, string, string, Exception> _connectionDropped =
                LoggerMessage.Define<string, string, string>(LogLevel.Error, new EventId(4, "ConnectionDropped"), "Connection to '{endpoint}' was dropped, probably caused by network instability or service restart. Will try to reconnect after the back off period. Error detail: {message}. Id: {ServiceConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _serviceConnectionClosed =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(14, "serviceConnectionClose"), "Service connection {ServiceConnectionId} closed.");

            private static readonly Action<ILogger, string, Exception> _readingCancelled =
                LoggerMessage.Define<string>(LogLevel.Trace, new EventId(15, "ReadingCancelled"), "Reading from service connection {ServiceConnectionId} cancelled.");

            private static readonly Action<ILogger, long, string, Exception> _receivedMessage =
                LoggerMessage.Define<long, string>(LogLevel.Debug, new EventId(16, "ReceivedMessage"), "Received {ReceivedBytes} bytes from service {ServiceConnectionId}.");

            private static readonly Action<ILogger, double, Exception> _startingKeepAliveTimer =
                LoggerMessage.Define<double>(LogLevel.Trace, new EventId(17, "StartingKeepAliveTimer"), "Starting keep-alive timer. Duration: {KeepAliveInterval:0.00}ms");

            private static readonly Action<ILogger, double, string, Exception> _serviceTimeout =
                LoggerMessage.Define<double, string>(LogLevel.Error, new EventId(18, "ServiceTimeout"), "Service timeout. {ServiceTimeout:0.00}ms elapsed without receiving a message from service. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, long, string, Exception> _writeMessageToApplication =
                LoggerMessage.Define<long, string>(LogLevel.Trace, new EventId(19, "WriteMessageToApplication"), "Writing {ReceivedBytes} to connection {TransportConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _serviceConnectionConnected =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(20, "ServiceConnectionConnected"), "Service connection {ServiceConnectionId} connected.");

            private static readonly Action<ILogger, Exception> _sendingHandshakeRequest =
                LoggerMessage.Define(LogLevel.Debug, new EventId(21, "SendingHandshakeRequest"), "Sending Handshake request to service.");

            private static readonly Action<ILogger, Exception> _handshakeComplete =
                LoggerMessage.Define(LogLevel.Debug, new EventId(22, "HandshakeComplete"), "Handshake with service completes.");

            private static readonly Action<ILogger, string, Exception> _errorReceivingHandshakeResponse =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(23, "ErrorReceivingHandshakeResponse"), "Error receiving handshake response. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, string, string, Exception> _handshakeError =
                LoggerMessage.Define<string, string>(LogLevel.Critical, new EventId(24, "HandshakeError"), "Service returned handshake error: {Error}. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, Exception> _sentPing =
                LoggerMessage.Define(LogLevel.Debug, new EventId(25, "SentPing"), "Sent a ping message to service.");

            private static readonly Action<ILogger, string, Exception> _failedSendingPing =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(26, "FailedSendingPing"), "Failed sending a ping message to service. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, string, string, Exception> _receivedServiceErrorMessage =
                LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(27, "ReceivedServiceErrorMessage"), "Connection {ServiceConnectionId} received error message from service: {Error}");

            private static readonly Action<ILogger, string, Exception> _unexpectedExceptionInStart =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(28, "UnexpectedExceptionInStart"), "Connection {ServiceConnectionId} got unexpected exception in StarAsync.");

            private static readonly Action<ILogger, string, Exception> _unexpectedExceptionInStop =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(29, "UnexpectedExceptionInStop"), "Connection {ServiceConnectionId} got unexpected exception in StopAsync.");

            private static readonly Action<ILogger, string, Exception> _onDemandConnectionHandshakeResponse =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(30, "OnDemandConnectionHandshakeResponse"), "Service returned handshake response: {Message}");

            public static void FailedToWrite(ILogger logger, string serviceConnectionId, Exception exception)
            {
                _failedToWrite(logger, exception.Message, serviceConnectionId, null);
            }

            public static void FailedToConnect(ILogger logger, string endpoint, string serviceConnectionId, Exception exception)
            {
                var message = exception.Message;
                var baseException = exception.GetBaseException();
                message += ". " + baseException.Message;

                _failedToConnect(logger, endpoint, message, serviceConnectionId, null);
            }

            public static void ErrorProcessingMessages(ILogger logger, string serviceConnectionId, Exception exception)
            {
                _errorProcessingMessages(logger, serviceConnectionId, exception);
            }

            public static void ConnectionDropped(ILogger logger, string endpoint, string serviceConnectionId, Exception exception)
            {
                var message = exception.Message;
                var baseException = exception.GetBaseException();
                message += ". " + baseException.Message;

                _connectionDropped(logger, endpoint, serviceConnectionId, message, null);
            }

            public static void ServiceConnectionClosed(ILogger logger, string serviceConnectionId)
            {
                _serviceConnectionClosed(logger, serviceConnectionId, null);
            }

            public static void ServiceConnectionConnected(ILogger logger, string serviceConnectionId)
            {
                _serviceConnectionConnected(logger, serviceConnectionId, null);
            }

            public static void ReadingCancelled(ILogger logger, string serviceConnectionId)
            {
                _readingCancelled(logger, serviceConnectionId, null);
            }

            public static void ReceivedMessage(ILogger logger, long bytes, string serviceConnectionId)
            {
                _receivedMessage(logger, bytes, serviceConnectionId, null);
            }

            public static void StartingKeepAliveTimer(ILogger logger, TimeSpan keepAliveInterval)
            {
                _startingKeepAliveTimer(logger, keepAliveInterval.TotalMilliseconds, null);
            }

            public static void ServiceTimeout(ILogger logger, TimeSpan serviceTimeout, string serviceConnectionId)
            {
                _serviceTimeout(logger, serviceTimeout.TotalMilliseconds, serviceConnectionId, null);
            }

            public static void SendingHandshakeRequest(ILogger logger)
            {
                _sendingHandshakeRequest(logger, null);
            }

            public static void HandshakeComplete(ILogger logger)
            {
                _handshakeComplete(logger, null);
            }

            public static void ErrorReceivingHandshakeResponse(ILogger logger, string serviceConnectionId, Exception exception)
            {
                _errorReceivingHandshakeResponse(logger, serviceConnectionId, exception);
            }

            public static void HandshakeError(ILogger logger, string error, string serviceConnectionId)
            {
                _handshakeError(logger, error, serviceConnectionId, null);
            }

            public static void OnDemandConnectionHandshakeResponse(ILogger logger, string message)
            {
                _onDemandConnectionHandshakeResponse(logger, message, null);
            }

            public static void SentPing(ILogger logger)
            {
                _sentPing(logger, null);
            }

            public static void FailedSendingPing(ILogger logger, string serviceConnectionId, Exception exception)
            {
                _failedSendingPing(logger, serviceConnectionId, exception);
            }

            public static void ReceivedServiceErrorMessage(ILogger logger, string connectionId, string errorMessage)
            {
                _receivedServiceErrorMessage(logger, connectionId, errorMessage, null);
            }

            public static void UnexpectedExceptionInStart(ILogger logger, string connectionId, Exception exception)
            {
                _unexpectedExceptionInStart(logger, connectionId, exception);
            }

            public static void UnexpectedExceptionInStop(ILogger logger, string connectionId, Exception exception)
            {
                _unexpectedExceptionInStop(logger, connectionId, exception);
            }
        }
    }
}