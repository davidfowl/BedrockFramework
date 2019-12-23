using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Middleware.Tls
{
    public interface ITlsApplicationProtocolFeature
    {
        ReadOnlyMemory<byte> ApplicationProtocol { get; }
    }
}
