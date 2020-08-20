using System.Buffers;
using System.IO.Pipelines;
using System.Net.Connections;
using System.Threading;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public static class Protocol
    {
        public static ProtocolWriter CreateWriter(this Connection connection)
            => new ProtocolWriter(connection.Pipe.Output);

        public static ProtocolWriter CreateWriter(this Connection connection, SemaphoreSlim semaphore)
            => new ProtocolWriter(connection.Pipe.Output, semaphore);

        public static ProtocolReader CreateReader(this Connection connection)
            => new ProtocolReader(connection.Pipe.Input);

        public static PipeReader CreatePipeReader(this Connection connection, IMessageReader<ReadOnlySequence<byte>> messageReader)
            => new MessagePipeReader(connection.Pipe.Input, messageReader);
    }
}
