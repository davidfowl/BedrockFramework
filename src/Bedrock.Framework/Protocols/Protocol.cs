using System.Threading;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public static class Protocol
    {
        public static ProtocolWriter CreateWriter(this ConnectionContext connection)
            => new ProtocolWriter(connection.Transport.Output);

        public static ProtocolWriter CreateWriter(this ConnectionContext connection, SemaphoreSlim semaphore)
            => new ProtocolWriter(connection.Transport.Output, semaphore);

        public static ProtocolReader CreateReader(this ConnectionContext connection)
            => new ProtocolReader(connection.Transport.Input);
    }
}
