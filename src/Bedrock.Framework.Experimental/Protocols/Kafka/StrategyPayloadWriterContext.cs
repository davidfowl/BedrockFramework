#nullable enable
#pragma warning disable CA1815 // Override equals and operator equals on value types

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public class StrategyPayloadWriterContext<TStrategy>
        where TStrategy : struct, IPayloadWriterStrategy
    {
        public readonly Dictionary<string, (long position, Memory<byte> memory)> SizeCalculations;
        public PipeWriter CurrentWriter => this.Pipe.Writer;

        public readonly Pipe Pipe;
        public int BytesWritten;

        public StrategyPayloadWriterContext(Pipe? pipe = null)
        {
            this.SizeCalculations = new Dictionary<string, (long, Memory<byte>)>();
            this.BytesWritten = 0;
            this.Pipe = pipe ?? new Pipe();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter<TStrategy> CreatePayloadWriter()
        {
            var context = this;

            return new StrategyPayloadWriter<TStrategy>(ref context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            this.CurrentWriter.Advance(count);
            this.BytesWritten += count;
        }
    }
}

#pragma warning restore CA1815 // Override equals and operator equals on value types
