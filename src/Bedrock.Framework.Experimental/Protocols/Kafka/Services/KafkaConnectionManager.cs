#nullable enable

using Microsoft.AspNetCore.Connections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Services
{
    public class KafkaConnectionManager : IKafkaConnectionManager
    {
        private readonly List<ConnectionContext> connections = new List<ConnectionContext>();
        public IEnumerable<ConnectionContext> Connections => this.connections;

        public bool TryAddConnection(ConnectionContext connection)
        {
            this.connections.Add(connection);

            return true;
        }

        public async ValueTask<bool> TryRemoveConnectionAsync(ConnectionContext connection)
        {
            if (connection == null)
            {
                return true;
            }

            if (!this.connections.Contains(connection))
            {
                return false;
            }

            await connection.DisposeAsync();

            return this.connections.Remove(connection);
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
        // ~KafkaConnectionManager()
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
