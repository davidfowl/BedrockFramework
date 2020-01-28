#nullable enable
#pragma warning disable CA1815 // Override equals and operator equals on value types

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public class PayloadWriterContext
    {
        public readonly Dictionary<string, (long position, Memory<byte> memory)> SizeCalculations;
        public readonly bool ShouldWriteBigEndian;
        public readonly Pipe Pipe;
        public int BytesWritten;

        public PayloadWriterContext(bool shouldWriteBigEndian, Pipe pipe)
        {
            this.SizeCalculations = new Dictionary<string, (long, Memory<byte>)>();
            this.ShouldWriteBigEndian = shouldWriteBigEndian;
            this.BytesWritten = 0;
            this.Pipe = pipe;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PayloadWriter CreatePayloadWriter()
        {
            var context = this;

            return new PayloadWriter(ref context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            this.Pipe.Writer.Advance(count);
            this.BytesWritten += count;
        }
    }
}

#pragma warning restore CA1815 // Override equals and operator equals on value types
