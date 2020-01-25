#nullable enable

using Microsoft.AspNetCore.Connections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Services
{
    public interface IKafkaConnectionManager : IDisposable
    {
        bool TryAddConnection(ConnectionContext connection);
        ValueTask<bool> TryRemoveConnectionAsync(ConnectionContext connection);
        IEnumerable<ConnectionContext> Connections { get; }
    }
}
