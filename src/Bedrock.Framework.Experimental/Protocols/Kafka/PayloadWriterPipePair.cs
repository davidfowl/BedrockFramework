#nullable enable
#pragma warning disable CA1815 // Override equals and operator equals on value types

using System.IO.Pipelines;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public readonly struct PayloadWriterPipePair
    {
        public IDuplexPipe Parent { get; }
        public IDuplexPipe Child { get; }

        public PayloadWriterPipePair(DuplexPipe.DuplexPipePair duplexPipePair)
            : this(toParent: duplexPipePair.Transport, toChild: duplexPipePair.Application)
        {
        }

        public PayloadWriterPipePair(IDuplexPipe toParent, IDuplexPipe toChild)
        {
            Parent = toParent;
            Child = toChild;
        }
    }
}

#pragma warning restore CA1815 // Override equals and operator equals on value types
