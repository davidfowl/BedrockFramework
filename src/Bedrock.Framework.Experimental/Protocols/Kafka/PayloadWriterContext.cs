#nullable enable
#pragma warning disable CA1815 // Override equals and operator equals on value types

using System;
using System.Collections.Generic;
using System.IO.Pipelines;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public struct PayloadWriterContext
    {
        public readonly Dictionary<string, (long position, Memory<byte> memory)> SizeCalculations;
        public readonly bool IsBigEndian;
        public int BytesWritten;
        public readonly Pipe Pipe;

        public PayloadWriterContext(bool isBigEndian, Pipe pipe)
        {
            this.SizeCalculations = new Dictionary<string, (long, Memory<byte>)>();
            this.IsBigEndian = isBigEndian;
            this.BytesWritten = 0;
            this.Pipe = pipe;
        }

        public PayloadWriter CreatePayloadWriter()
        {
            var context = this;

            return new PayloadWriter(ref context);
        }

        public void Advance(int count)
        {
            this.Pipe.Writer.Advance(count);
            this.BytesWritten += count;
        }
    }
}

#pragma warning restore CA1815 // Override equals and operator equals on value types
