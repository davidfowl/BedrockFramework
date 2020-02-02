#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using Bedrock.Framework.Experimental.Protocols.Kafka.Primitives;
using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests
{
    public abstract class KafkaRequest : IDisposable
    {
        protected KafkaRequest(KafkaApiKeys apiKey, short apiVersion)
        {
            this.ApiKey = apiKey;
            this.ApiVersion = apiVersion;
        }

        public KafkaApiKeys ApiKey { get; }
        public short ApiVersion { get; }

        public NullableString ClientId { get; set; }

        public abstract void WriteRequest<TStrategy>(ref StrategyPayloadWriter<TStrategy> writer)
            where TStrategy : struct, IPayloadWriterStrategy;

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

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
