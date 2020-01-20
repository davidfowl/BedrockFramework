using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.IO.Pipelines;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public ref struct PayloadWriter
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        private readonly static PipeOptions pipeOptions = new PipeOptions();

//        private IMemoryOwner<byte> memoryPool;
        private readonly DuplexPipe.DuplexPipePair duplexPipe;
        private readonly BufferWriter<IBufferWriter<byte>> internalWriter;

        private BufferWriter<IBufferWriter<byte>> currentWriter;

        public readonly BufferWriter<IBufferWriter<byte>> BufferWriter;

        public bool IsBigEndian;
        public bool ShouldCalculatePayloadSize;

        public PayloadWriter(
            in BufferWriter<IBufferWriter<byte>> bufferWriter,
            bool shouldCalculateSizeBeforeWriting,
            bool isBigEndian)
        {
            this.ShouldCalculatePayloadSize = shouldCalculateSizeBeforeWriting;
            this.IsBigEndian = isBigEndian;

            this.BufferWriter = bufferWriter;
            this.duplexPipe = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);
            this.internalWriter = new BufferWriter<IBufferWriter<byte>>(PipeWriter.Create(this.duplexPipe.Transport.Output.AsStream()));

            this.currentWriter = shouldCalculateSizeBeforeWriting
                ? this.internalWriter
                : this.BufferWriter;
        }

        public PayloadWriter WriteCalculatedSize()
        {
            if (this.IsBigEndian)
            {
            }
            else
            {

            }

            return this;
        }

        public PayloadWriter Write(int value)
        {
            if (this.IsBigEndian)
            {
                var intSpan = this.BufferWriter.Span.Slice(0, sizeof(int));
                this.currentWriter.WriteInt32BigEndian(value);
            }
            else
            {
                // TODO: little endian
                //this.currentWriter.WriteInt32LittleEndian(this.duplexPipe.Transport.Output.GetSpan(), value);
            }

            return this;
        }

        public bool TryWritePayload(out ReadOnlySpan<byte> payload)
        {
            return this.ShouldCalculatePayloadSize
                ? this.WriteFromCalculatedSizeBuffer(out payload)
                : this.WriteNormalBuffer(out payload);
        }

        private bool WriteNormalBuffer(out ReadOnlySpan<byte> payload)
        {
            payload = ReadOnlySpan<byte>.Empty;

            return true;
        }

        private bool WriteFromCalculatedSizeBuffer(out ReadOnlySpan<byte> payload)
        {
            payload = ReadOnlySpan<byte>.Empty;


            return true;
        }
    }
}