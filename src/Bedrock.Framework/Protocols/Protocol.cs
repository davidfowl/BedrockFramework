using System.Threading;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public static class Protocol
    {
        public static ProtocolWriter<TWriteMessage> CreateWriter<TWriteMessage>(this ConnectionContext connection, IProtocolWriter<TWriteMessage> writer)
            => new ProtocolWriter<TWriteMessage>(connection, writer);

        public static ProtocolWriter<TWriteMessage> CreateWriter<TWriteMessage>(this ConnectionContext connection, IProtocolWriter<TWriteMessage> writer, SemaphoreSlim semaphore)
            => new ProtocolWriter<TWriteMessage>(connection, writer, semaphore);

        public static ProtocolReader<TReadMessage> CreateReader<TReadMessage>(this ConnectionContext connection, IProtocolReader<TReadMessage> reader, int? maximumMessageSize = null)
            => new ProtocolReader<TReadMessage>(connection, reader, maximumMessageSize);
    }
}
