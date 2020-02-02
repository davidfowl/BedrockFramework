#nullable enable
#pragma warning disable CA1815 // Override equals and operator equals on value types

using System;
using System.Buffers;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public ref struct StrategyPayloadWriter<TStrategy>
        where TStrategy : struct, IPayloadWriterStrategy
    {
        private StrategyPayloadWriterContext<TStrategy> context;
        public StrategyPayloadWriterContext<TStrategy> Context
        {
            get
            {
                if (this.context != null)
                {
                    return this.context;
                }
                
                this.context = new StrategyPayloadWriterContext<TStrategy>();

                return this.context;
            }

            set => this.context = value;
        }

        /// <summary>
        /// Initializes a new child instance of the <see cref="StrategyPayloadWriter"/>,
        /// from the context of another <see cref="StrategyPayloadWriter"/>.
        /// </summary>
        /// <param name="context">The context of the parent writer.</param>
        public StrategyPayloadWriter(ref StrategyPayloadWriterContext<TStrategy> context)
        {
            this.context = context;

            // Hook up the parent's pipe reader to this writer, and vice-versa.
            // this.ToParent = PipeWriter.Create(this.context.Pipe.Reader.AsStream());
            // this.FromChild = PipeReader.Create(this.context.Pipe.Writer.AsStream());
        }

        /// <summary>
        /// Sets the location in a payload where a size will be calculated.
        /// Calculated size is between <see cref="StartCalculatingSize(string)"/>
        /// and a call to <see cref="EndSizeCalculation(string)"/> with the same name.
        /// </summary>
        /// <param name="name">The distinct name of a size calculation.</param>
        /// <returns>The <see cref="PayloadWriter"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter<TStrategy> StartCalculatingSize(string name)
        {
            if (this.Context.SizeCalculations.ContainsKey(name))
            {
                throw new ArgumentException($"Unable to add another size calculation called: {name}", nameof(name));
            }

            var memory = this.Context.CurrentWriter
                .GetMemory(sizeof(int))
                .Slice(0, sizeof(int));

            // Size calculation is not inclusive of the size value itself, + sizeof(int) starts
            // the calculation _after_ where the size value would be.
            this.Context.SizeCalculations[name] = (this.Context.BytesWritten + sizeof(int), memory);

            this.Context.Advance(sizeof(int));

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter<TStrategy> EndSizeCalculation(string name)
        {
            if (!this.Context.SizeCalculations.TryGetValue(name, out var calculation))
            {
                throw new ArgumentException($"Size calculation for {name} not found", nameof(name));
            }

            this.Context.SizeCalculations.Remove(name);
            var currentPosition = this.Context.BytesWritten;
            var size = (int)(currentPosition - calculation.position);

            var span = calculation.memory.Span;
            default(TStrategy).WriteInt32(span, size);

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter<TStrategy> Write(ReadOnlySpan<byte> bytes)
        {
            this.Context.CurrentWriter.Write(bytes);
            this.Context.Advance(bytes.Length);

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter<TStrategy> Write(Action<StrategyPayloadWriterContext<TStrategy>> action)
        {
            action(this.Context);

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter<TStrategy> Write(short value)
        {
            var span = this.Context.CurrentWriter.GetSpan(sizeof(short));
            default(TStrategy).WriteInt16(span, value);

            this.Context.Advance(sizeof(short));

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter<TStrategy> Write(byte value)
        {
            var span = this.Context.CurrentWriter.GetSpan(sizeof(byte));
            default(TStrategy).WriteByte(span, value);

            this.Context.Advance(sizeof(byte));

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter<TStrategy> Write(long value)
        {
            var span = this.Context.CurrentWriter.GetSpan(sizeof(long));
            default(TStrategy).WriteInt64(span, value);

            this.Context.Advance(sizeof(long));

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter<TStrategy> Write(int value)
        {
            var span = this.Context.CurrentWriter.GetSpan(sizeof(int));
            default(TStrategy).WriteInt32(span, value);

            this.Context.Advance(sizeof(int));

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryWritePayload(out ReadOnlySequence<byte> payload)
        {
            if (this.Context.SizeCalculations.Any())
            {
                throw new InvalidOperationException($"Not all size calculations have been closed. Call {nameof(PayloadWriter.EndSizeCalculation)} for: {string.Join(',', this.Context.SizeCalculations.Keys)}");
            }

            this.Context.CurrentWriter.Complete();

            return this.WriteOutput(out payload);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriteOutput(out ReadOnlySequence<byte> payload)
        {
            while (this.Context.Pipe.Reader.TryRead(out var result))
            {
                if (!result.IsCompleted)
                {
                    continue;
                }

                var scopedSpan = result.Buffer.Slice(0, this.Context.BytesWritten);
                var output = new byte[this.Context.BytesWritten];
                scopedSpan.CopyTo(output);

                payload = new ReadOnlySequence<byte>(output);

                this.Context.Pipe.Reader.Complete();

                return true;
            }

            payload = ReadOnlySequence<byte>.Empty;

            return false;
        }
    }
}

#pragma warning restore CA1815 // Override equals and operator equals on value types
