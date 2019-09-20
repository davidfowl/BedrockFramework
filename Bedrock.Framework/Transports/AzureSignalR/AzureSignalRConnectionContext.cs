using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;

namespace Bedrock.Framework
{
    internal class AzureSignalRConnectionContext : ConnectionContext,
                                              IConnectionUserFeature,
                                              IConnectionItemsFeature,
                                              IConnectionIdFeature,
                                              IConnectionTransportFeature,
                                              IConnectionHeartbeatFeature,
                                              IConnectionLifetimeFeature
    {
        private static readonly PipeOptions DefaultPipeOptions = new PipeOptions(pauseWriterThreshold: 0,
            resumeWriterThreshold: 0,
            readerScheduler: PipeScheduler.ThreadPool,
            useSynchronizationContext: false);

        private readonly AzureSignalRConnectionListener _listener;
        private readonly object _heartbeatLock = new object();
        private List<(Action<object> handler, object state)> _heartbeatHandlers;
        private Task _processingTask;
        private bool _disconnectReceived;

        public AzureSignalRConnectionContext(OpenConnectionMessage serviceMessage, AzureSignalRConnectionListener listener,  PipeOptions transportPipeOptions = null, PipeOptions appPipeOptions = null)
        {
            ConnectionId = serviceMessage.ConnectionId;
            _listener = listener;
            // User = serviceMessage.GetUserPrincipal();

            // Create the Duplix Pipeline for the virtual connection
            transportPipeOptions ??= DefaultPipeOptions;
            appPipeOptions ??= DefaultPipeOptions;

            var pair = DuplexPipe.CreateConnectionPair(transportPipeOptions, appPipeOptions);
            Transport = pair.Application;
            Application = pair.Transport;

            Features = BuildFeatures();
        }

        public void OnHeartbeat(Action<object> action, object state)
        {
            lock (_heartbeatLock)
            {
                if (_heartbeatHandlers == null)
                {
                    _heartbeatHandlers = new List<(Action<object> handler, object state)>();
                }
                _heartbeatHandlers.Add((action, state));
            }
        }

        internal void Start()
        {
            _processingTask = ProcessOutgoingMessagesAsync();
        }

        internal async Task<bool> ProcessHandshakeAsync()
        {
            // This is a limitation of the current implementation, we need to eat the signalr handshake protocol
            // The service lets this get through to the application layer but we don't want to leak it.
            while (true)
            {
                var result = await Transport.Input.ReadAsync();
                var buffer = result.Buffer;
                var consumed = buffer.Start;
                var examined = buffer.End;
                try
                {
                    if (!buffer.IsEmpty)
                    {
                        if (HandshakeProtocol.TryParseRequestMessage(ref buffer, out _))
                        {
                            // We parsed the handshake
                            consumed = buffer.Start;
                            examined = consumed;
                            break;
                        }
                    }

                    if (result.IsCompleted)
                    {
                        // The connection closed before we were able to parse the handshake
                        // Don't expose it as an accepted connection
                        await DisposeAsync();
                        return false;
                    }
                }
                finally
                {
                    Transport.Input.AdvanceTo(consumed, examined);
                }
            }

            return true;
        }

        public void TickHeartbeat()
        {
            lock (_heartbeatLock)
            {
                if (_heartbeatHandlers == null)
                {
                    return;
                }

                foreach (var (handler, state) in _heartbeatHandlers)
                {
                    handler(state);
                }
            }
        }

        public override string ConnectionId { get; set; }

        public override IFeatureCollection Features { get; }

        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems(new ConcurrentDictionary<object, object>());

        public override IDuplexPipe Transport { get; set; }

        public IDuplexPipe Application { get; set; }

        public ClaimsPrincipal User { get; set; }

        private FeatureCollection BuildFeatures()
        {
            var features = new FeatureCollection();
            features.Set<IConnectionHeartbeatFeature>(this);
            features.Set<IConnectionUserFeature>(this);
            features.Set<IConnectionItemsFeature>(this);
            features.Set<IConnectionIdFeature>(this);
            features.Set<IConnectionTransportFeature>(this);
            return features;
        }

        public override async ValueTask DisposeAsync()
        {
            Transport.Input.Complete();
            Transport.Output.Complete();

            await (_processingTask ?? Task.CompletedTask);

            if (!_disconnectReceived)
            {
                // Send a close message to the service, this should eventually raise a disconnect message
                // so we don't have to clean anything up here
                await _listener.WriteAsync(new CloseConnectionMessage(ConnectionId));
            }
        }

        public void Disconnect()
        {
            _disconnectReceived = true;
            Application.Output.Complete();
        }

        private async Task ProcessOutgoingMessagesAsync()
        {
            try
            {
                while (true)
                {
                    var result = await Application.Input.ReadAsync();
                    if (result.IsCanceled)
                    {
                        break;
                    }

                    var buffer = result.Buffer;
                    if (!buffer.IsEmpty)
                    {
                        try
                        {
                            // Forward the message to the service
                            await _listener.WriteAsync(new ConnectionDataMessage(ConnectionId, buffer));
                        }
                        catch (Exception)
                        {
                            // Log.ErrorSendingMessage(Logger, ex);
                        }
                    }

                    if (result.IsCompleted)
                    {
                        // This connection ended (the application itself shut down) we should remove it from the list of connections
                        break;
                    }

                    Application.Input.AdvanceTo(buffer.End);
                }
            }
            catch (Exception)
            {
                // The exception means application fail to process input anymore
                // Cancel any pending flush so that we can quit and perform disconnect
                // Here is abort close and WaitOnApplicationTask will send close message to notify client to disconnect
                // Log.SendLoopStopped(Logger, connection.ConnectionId, ex);
                Application.Output.CancelPendingFlush();
            }

            Application.Input.Complete();
        }
    }
}
