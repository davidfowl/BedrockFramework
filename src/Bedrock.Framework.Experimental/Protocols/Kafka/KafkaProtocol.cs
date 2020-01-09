using Bedrock.Framework.Experimental.Protocols.Kafka.Messages;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public class KafkaProtocol
    {
        private readonly ConnectionContext connection;
        
        private readonly ProtocolReader reader;
        private readonly ProtocolWriter writer;

        private readonly KafkaMessageReader messageReader;
        private readonly KafkaMessageWriter messageWriter;

        private readonly ConcurrentDictionary<int, KafkaRequest> correlations =
            new ConcurrentDictionary<int, KafkaRequest>();

        public KafkaProtocol(string clientId, ConnectionContext connection)
        {
            this.ClientId = clientId;
        
            this.connection = connection;

            this.messageReader = new KafkaMessageReader(this.correlations);
            this.messageWriter = new KafkaMessageWriter(this.correlations, this.ClientId);

            this.reader = connection.CreateReader();
            this.writer = connection.CreateWriter();
        }

        public string ClientId { get; }

        public async ValueTask<KafkaResponse> SendAsync(KafkaRequest request, CancellationToken token = default)
        {
            await this.writer.WriteAsync(this.messageWriter, request, token).ConfigureAwait(false);
            var result = await this.reader.ReadAsync(this.messageReader, token).ConfigureAwait(false);

            if (result.IsCompleted)
            {
                throw new ConnectionAbortedException();
            }

            this.reader.Advance();

            return result.Message;
        }
    }
}
