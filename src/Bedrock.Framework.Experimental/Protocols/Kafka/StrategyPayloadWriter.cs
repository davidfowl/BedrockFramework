#nullable enable
#pragma warning disable CA1815 // Override equals and operator equals on value types

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public ref struct StrategyPayloadWriter
    {
        public readonly PipeWriter ToParent;
        public readonly PipeReader FromChild;
        public readonly PipeWriter CurrentWriter;

        public StrategyPayloadWriterContext Context;
        private readonly IPayloadWriterStrategy strategy;

        /// <summary>
        /// Initializes a new child instance of the <see cref="StrategyPayloadWriter"/>,
        /// from the context of another <see cref="StrategyPayloadWriter"/>.
        /// </summary>
        /// <param name="settings">The context of the parent writer.</param>
        public StrategyPayloadWriter(ref StrategyPayloadWriterContext settings)
        {
            this.Context = settings;
            this.strategy = this.Context.WritingStrategy;

            // Hook up the parent's pipe reader to this writer, and vice-versa.
            this.ToParent = PipeWriter.Create(this.Context.Pipe.Reader.AsStream());
            this.FromChild = PipeReader.Create(this.Context.Pipe.Writer.AsStream());

            this.CurrentWriter = this.Context.Pipe.Writer;
        }

        /// <summary>
        /// Creates a root instance of the <see cref="PayloadWriter"/> struct.
        /// </summary>
        /// <param name="shouldWriteBigEndian">Whether or not to write bytes as big endian. Defaults to true.</param>
        public StrategyPayloadWriter(bool shouldWriteBigEndian)
        {
            var pipe = new Pipe();
            this.ToParent = pipe.Writer;
            this.FromChild = pipe.Reader;

            this.Context = new StrategyPayloadWriterContext(
                shouldWriteBigEndian
                ? new BigEndianStrategy()
                : (IPayloadWriterStrategy)new LittleEndianStrategy(),
                pipe);

            this.strategy = this.Context.WritingStrategy;

            // Root writer, so route writer to itself.
            this.CurrentWriter = this.ToParent;
        }

        /// <summary>
        /// Sets the location in a payload where a size will be calculated.
        /// Calculated size is between <see cref="StartCalculatingSize(string)"/>
        /// and a call to <see cref="EndSizeCalculation(string)"/> with the same name.
        /// </summary>
        /// <param name="name">The distinct name of a size calculation.</param>
        /// <returns>The <see cref="PayloadWriter"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter StartCalculatingSize(string name)
        {
            if (this.Context.SizeCalculations.ContainsKey(name))
            {
                throw new ArgumentException($"Unable to add another size calculation called: {name}", nameof(name));
            }

            var memory = this.CurrentWriter
                .GetMemory(sizeof(int))
                .Slice(0, sizeof(int));

            // Size calculation is not inclusive of the size value itself, + sizeof(int) starts
            // the calculation _after_ where the size value would be.
            this.Context.SizeCalculations[name] = (this.Context.BytesWritten + sizeof(int), memory);

            this.Context.Advance(sizeof(int));

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter EndSizeCalculation(string name)
        {
            if (!this.Context.SizeCalculations.TryGetValue(name, out var calculation))
            {
                throw new ArgumentException($"Size calculation for {name} not found", nameof(name));
            }

            this.Context.SizeCalculations.Remove(name);
            var currentPosition = this.Context.BytesWritten;
            var size = (int)(currentPosition - calculation.position);

            var span = calculation.memory.Span;

            this.strategy.WriteInt32(span, size);

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter Write(ReadOnlySpan<byte> bytes)
        {
            this.CurrentWriter.Write(bytes);
            this.Context.Advance(bytes.Length);

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter Write(Action<StrategyPayloadWriterContext> action)
        {
            action(this.Context);

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter Write(short value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(short));

            this.strategy.WriteInt16(span, value);

            this.Context.Advance(sizeof(short));

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter Write(byte value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(byte));
            this.strategy.WriteByte(span, value);

            this.Context.Advance(sizeof(byte));

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter Write(long value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(long));

            this.strategy.WriteInt64(span, value);
            this.Context.Advance(sizeof(long));

            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StrategyPayloadWriter Write(int value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(int));
            this.strategy.WriteInt32(span, value);

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

            this.CurrentWriter.Complete();

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
