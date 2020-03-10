using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp.Methods
{
    public abstract class MethodBase
    {
        public abstract byte ClassId { get; }
        public abstract byte MethodId { get; }
    }
}
