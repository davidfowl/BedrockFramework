#nullable enable
#pragma warning disable CA1815 // Override equals and operator equals on value types

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public ref struct PayloadWriter
    {
        public readonly PipeWriter ToParent;
        public readonly PipeReader FromChild;
        public readonly PipeWriter CurrentWriter;

        public PayloadWriterContext Context;

        /// <summary>
        /// Initializes a new child instance of the <see cref="PayloadWriter"/>,
        /// from the context of another <see cref="PayloadWriter"/>.
        /// </summary>
        /// <param name="settings">The context of the parent writer.</param>
        public PayloadWriter(ref PayloadWriterContext settings)
        {
            this.Context = settings;

            // Hook up the parent's pipe reader to this writer, and vice-versa.
            this.ToParent = PipeWriter.Create(this.Context.Pipe.Reader.AsStream());
            this.FromChild = PipeReader.Create(this.Context.Pipe.Writer.AsStream());

            this.CurrentWriter = this.Context.Pipe.Writer;
        }

        /// <summary>
        /// Creates a root instance of the <see cref="PayloadWriter"/> struct.
        /// </summary>
        /// <param name="isBigEndian">Whether or not to write bytes as big endian. Defaults to true.</param>
        public PayloadWriter(bool isBigEndian)
        {
            var pipe = new Pipe();
            this.ToParent = pipe.Writer;
            this.FromChild = pipe.Reader;

            this.Context = new PayloadWriterContext(
                isBigEndian,
                pipe);

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
        public PayloadWriter StartCalculatingSize(string name)
        {
            if (this.Context.SizeCalculations.ContainsKey(name))
            {
                throw new ArgumentException($"Unable to add another size calculation called: {name}", nameof(name));
            }

            var memory = this.CurrentWriter
                .GetMemory(sizeof(int))
                .Slice(0, sizeof(int));

            this.Context.SizeCalculations[name] = (this.Context.BytesWritten, memory);

            this.Context.Advance(sizeof(int));

            return this;
        }

        public PayloadWriter EndSizeCalculation(string name)
        {
            if (!this.Context.SizeCalculations.TryGetValue(name, out var calculation))
            {
                throw new ArgumentException($"Size calculation for {name} not found", nameof(name));
            }

            this.Context.SizeCalculations.Remove(name);

            var currentPosition = this.Context.BytesWritten;
            var size = (int)(currentPosition - calculation.position);

            var span = calculation.memory.Span;

            if (this.Context.IsBigEndian)
            {
                BinaryPrimitives.WriteInt32BigEndian(span, size);
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(span, size);
            }

            return this;
        }

        public PayloadWriter Write(ReadOnlySpan<byte> bytes)
        {
            this.CurrentWriter.Write(bytes);
            this.Context.Advance(bytes.Length);

            return this;
        }

        public PayloadWriter Write(Action<PayloadWriterContext> action)
        {
            action(this.Context);

            return this;
        }

        public PayloadWriter Write(short value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(short));

            if (this.Context.IsBigEndian)
            {
                BinaryPrimitives.WriteInt16BigEndian(span, value);
            }
            else
            {
                BinaryPrimitives.WriteInt16LittleEndian(span, value);
            }

            this.Context.Advance(sizeof(short));

            return this;
        }

        public PayloadWriter Write(byte value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(byte));
            span[0] = value;

            this.Context.Advance(sizeof(byte));

            return this;
        }

        public PayloadWriter Write(long value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(long));

            if (this.Context.IsBigEndian)
            {
                BinaryPrimitives.WriteInt64BigEndian(span, value);
            }
            else
            {
                BinaryPrimitives.WriteInt64LittleEndian(span, value);
            }

            this.Context.Advance(sizeof(long));

            return this;
        }

        public PayloadWriter Write(int value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(int));

            if (this.Context.IsBigEndian)
            {
                BinaryPrimitives.WriteInt32BigEndian(span, value);
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(span, value);
            }

            this.Context.Advance(sizeof(int));

            return this;
        }

        public bool TryWritePayload(out ReadOnlySequence<byte> payload)
        {
            this.CurrentWriter.Complete();

            return this.WriteOutput(out payload);
        }

        private bool WriteOutput(out ReadOnlySequence<byte> payload)
        {
            while (this.Context.Pipe.Reader.TryRead(out var result))
            {
                if (!result.IsCompleted)
                {
                    continue;
                }

                var output = new byte[result.Buffer.Length];
                result.Buffer.CopyTo(output);
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
