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

        public abstract void WriteRequest(ref PayloadWriter writer);

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
        // ~KafkaRequest()
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
