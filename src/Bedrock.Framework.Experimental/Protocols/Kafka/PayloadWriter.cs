#nullable enable
#pragma warning disable CA1815 // Override equals and operator equals on value types

using Bedrock.Framework.Experimental.Protocols.Kafka;
using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public ref struct PayloadWriter
    {
        public readonly PipeWriter ToParent;
        public readonly PipeReader FromChild;
        public readonly PipeWriter CurrentWriter;

        public PayloadWriterContext Settings;

        /// <summary>
        /// Initializes a new child instance of the <see cref="PayloadWriter"/>,
        /// from the context of another <see cref="PayloadWriter"/>.
        /// </summary>
        /// <param name="settings">The context of the parent writer.</param>
        public PayloadWriter(ref PayloadWriterContext settings)
        {
            this.Settings = settings;

            this.ToParent = PipeWriter.Create(this.Settings.Pipe.Reader.AsStream());
            this.FromChild = PipeReader.Create(this.Settings.Pipe.Writer.AsStream());

            this.CurrentWriter = this.Settings.Pipe.Writer;
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

            this.Settings = new PayloadWriterContext(
                isBigEndian,
                pipe);

            // Root writer, so route writer to itself.
            this.CurrentWriter = this.ToParent;
        }

        public PayloadWriter StartCalculatingSize(string name)
        {
            if (this.Settings.SizeCalculations.ContainsKey(name))
            {
                throw new ArgumentException($"Unable to add another size calculation called: {name}", nameof(name));
            }

            var memory = this.CurrentWriter
                .GetMemory(sizeof(int))
                .Slice(0, sizeof(int));

            this.Settings.SizeCalculations[name] = (this.Settings.BytesWritten, memory);

            this.Settings.Advance(sizeof(int));

            return this;
        }

        public PayloadWriter EndSizeCalculation(string name)
        {
            if (!this.Settings.SizeCalculations.TryGetValue(name, out var calculation))
            {
                throw new ArgumentException($"Size calculation for {name} not found", nameof(name));
            }

            this.Settings.SizeCalculations.Remove(name);

            var currentPosition = this.Settings.BytesWritten;
            var size = (int)(currentPosition - calculation.position);

            var span = calculation.memory.Span;

            if (this.Settings.IsBigEndian)
            {
                BinaryPrimitives.WriteInt32BigEndian(span, size);
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(span, size);
            }

            return this;
        }

        public PayloadWriter Write(Action<PayloadWriterContext> action)
        {
            action(this.Settings);

            return this;
        }

        public PayloadWriter Write(byte[]? bytes, int? length)
        {
            //this.internalWriter.WriteBytes(ref bytes, length);

            return this;
        }

        public PayloadWriter Write(short value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(short));

            if (this.Settings.IsBigEndian)
            {
                BinaryPrimitives.WriteInt16BigEndian(span, value);
            }
            else
            {
                BinaryPrimitives.WriteInt16LittleEndian(span, value);
            }

            this.Settings.Advance(sizeof(short));

            return this;
        }

        public PayloadWriter Write(byte value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(byte));
            span[0] = value;

            this.Settings.Advance(sizeof(byte));

            return this;
        }

        public PayloadWriter Write(long value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(long));

            if (this.Settings.IsBigEndian)
            {
                BinaryPrimitives.WriteInt64BigEndian(span, value);
            }
            else
            {
                BinaryPrimitives.WriteInt64LittleEndian(span, value);
            }

            this.Settings.Advance(sizeof(long));

            return this;
        }

        public PayloadWriter Write(int value)
        {
            var span = this.CurrentWriter.GetSpan(sizeof(int));

            if (this.Settings.IsBigEndian)
            {
                BinaryPrimitives.WriteInt32BigEndian(span, value);
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(span, value);
            }

            this.Settings.Advance(sizeof(int));

            return this;
        }

        public bool TryWritePayload(out ReadOnlySequence<byte> payload)
        {
            this.CurrentWriter.Complete();

            return this.WriteOutput(out payload);
        }

        private bool WriteOutput(out ReadOnlySequence<byte> payload)
        {
            while (this.Settings.Pipe.Reader.TryRead(out var result))
            {
                if (!result.IsCompleted)
                {
                    continue;
                }

                var output = new byte[result.Buffer.Length];
                result.Buffer.CopyTo(output);
                payload = new ReadOnlySequence<byte>(output);

                this.Settings.Pipe.Reader.Complete();

                return true;
            }

            payload = ReadOnlySequence<byte>.Empty;

            return false;
        }

    }
}

#pragma warning restore CA1815 // Override equals and operator equals on value types
