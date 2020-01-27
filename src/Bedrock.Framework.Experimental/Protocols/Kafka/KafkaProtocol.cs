#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests;
using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses;
using Bedrock.Framework.Experimental.Protocols.Kafka.Primitives;
using Bedrock.Framework.Experimental.Protocols.Kafka.Services;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public class KafkaProtocol : IDisposable
    {
#pragma warning disable CA1034 // Nested types should not be visible
        public static class Keys
#pragma warning restore CA1034 // Nested types should not be visible
        {
            public const string ClientId = nameof(ClientId);

            // Precomputed ClientId as a Kafka Nullable string.
            public const string ClientIdNullable = nameof(ClientIdNullable);
            public const string Reader = nameof(Reader);
            public const string Writer = nameof(Writer);
            public const string ApiVersions = nameof(ApiVersions);
        }

        private readonly ConcurrentDictionary<ConnectionContext, (ProtocolReader reader, ProtocolWriter writer)> readerWriters
            = new ConcurrentDictionary<ConnectionContext, (ProtocolReader reader, ProtocolWriter writer)>();

        private readonly KafkaMessageReader messageReader;
        private readonly KafkaMessageWriter messageWriter;
        private readonly IServiceProvider services;
        private readonly ILogger<KafkaProtocol> logger;
        private readonly IKafkaConnectionManager connectionManager;

        public KafkaProtocol(
            IServiceProvider serviceProvider,
            ILogger<KafkaProtocol> logger,
            IKafkaConnectionManager connectionManager,
            KafkaMessageReader reader,
            KafkaMessageWriter writer)
        {
            this.services = serviceProvider;
            this.logger = logger;

            this.connectionManager = connectionManager;
            this.messageReader = reader;
            this.messageWriter = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public async ValueTask<TResponse> SendAsync<TRequest, TResponse>(ConnectionContext connection, TRequest request, CancellationToken token = default)
            where TRequest : KafkaRequest
            where TResponse : KafkaResponse
        {
            if (!this.readerWriters.TryGetValue(connection, out var readerWriter))
            {
                throw new ArgumentException("Unable to find connection in manager. Was it registered?", nameof(connection));
            }

            var (reader, writer) = readerWriter;
            request.ClientId = (NullableString)connection.Items[KafkaProtocol.Keys.ClientIdNullable];

            await writer.WriteAsync(this.messageWriter, request, token).ConfigureAwait(false);

            var result = await reader.ReadAsync(this.messageReader, token).ConfigureAwait(false);

            if (result.IsCompleted)
            {
                throw new ConnectionAbortedException();
            }

            if (result.Message is NullResponse)
            {
                throw new InvalidOperationException($"Got back {nameof(NullResponse)} for {request.GetType().FullName}");
            }

            reader.Advance();

            return result.Message as TResponse ?? throw new NullReferenceException($"Received invalid response back from KafkaRequest: {request.GetType().FullName}");
        }

        public async ValueTask SetClientConnectionAsync(ConnectionContext connection, string clientId)
        {
            connection.Items.Add(KafkaProtocol.Keys.ClientId, clientId);
            connection.Items.Add(KafkaProtocol.Keys.ClientIdNullable, new NullableString(clientId));

            var reader = connection.CreateReader();
            var writer = connection.CreateWriter();

            // Cache the reader and writer for easy access...
            this.readerWriters.TryAdd(connection, (reader, writer));

            // And also associate them with the connection itself.
            connection.Items.Add(KafkaProtocol.Keys.Reader, reader);
            connection.Items.Add(KafkaProtocol.Keys.Writer, writer);

            await this.StartupConnectionAsync(connection, connection.ConnectionClosed).ConfigureAwait(false);
            this.connectionManager.TryAddConnection(connection);

            this.logger.LogInformation("{ConnectionId}: Added {Connection} to ConnectionManager", connection.ConnectionId, connection);
        }

        private async ValueTask StartupConnectionAsync(ConnectionContext connection, CancellationToken token = default)
        {
            // Send the lowest ApiVersionRequest - this should work for any broker
            var lowestApiLevels = await this.SendAsync<ApiVersionsRequestV0, ApiVersionsResponseV0>(connection, ApiVersionsRequestV0.AllSupportedApis, token).ConfigureAwait(false);

            Debug.Assert(lowestApiLevels.SupportedApis.Any());

            // Store allowed api messages and their versions on the connection itself.
            connection.Items.Add(KafkaProtocol.Keys.ApiVersions, lowestApiLevels.SupportedApis);

            this.logger.LogInformation($"{connection.ConnectionId}: Retrieved ApiKeys from {connection.RemoteEndPoint}");
            if (this.logger.IsEnabled(LogLevel.Debug))
            {
                var sb = new StringBuilder();
                foreach (var api in lowestApiLevels.SupportedApis)
                {
                    sb.AppendLine($"{connection.ConnectionId}: {api.ApiKey} Min: {api.MinimumVersion} Max: {api.MaximumVersion}");
                }

                this.logger.LogDebug(sb.ToString());
            }

            var metadataResponse = await this.SendAsync<MetadataRequestV0, MetadataResponseV0>(
                connection,
                MetadataRequestV0.AllTopics)
                .ConfigureAwait(false);

            Debug.Assert(metadataResponse.Brokers.Any());
            Debug.Assert(metadataResponse.Topics.Any());

            this.logger.LogInformation($"{connection.ConnectionId}: Retrieved Metadata from {connection.RemoteEndPoint}");
            if (this.logger.IsEnabled(LogLevel.Debug))
            {
                var sb = new StringBuilder(capacity: metadataResponse.Brokers.Length + metadataResponse.Topics.Sum(t => t.Partitions.Length));
                foreach (var broker in metadataResponse.Brokers)
                {
                    sb.AppendLine($"{connection.ConnectionId}: Broker: {broker.Host}:{broker.Port}: NodeId: {broker.NodeId}");
                }

                var indent = new string(' ', 4);
                foreach (var topic in metadataResponse.Topics)
                {
                    sb.AppendLine($"{indent}{topic.Name}: Partitions: {topic.Partitions.Length}");
                }
            }

            // TODO: get all brokers, and establish connections for them.
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~KafkaProtocol()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
