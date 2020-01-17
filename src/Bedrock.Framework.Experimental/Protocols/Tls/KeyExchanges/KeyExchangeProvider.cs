using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.KeyExchanges
{
    internal abstract class KeyExchangeProvider
    {
        public abstract KeyExchange GetKeyExchange(KeyExchangeType keyExchange, ReadOnlySequence<byte> supportedGroups);
        public abstract KeyExchange GetKeyExchangeFromKeyShareExtension(ReadOnlySequence<byte> keyShare);
        public abstract KeyExchange GetKeyExchangeFromSupportedGroups(ReadOnlySequence<byte> supportedGroups);
    }
}
