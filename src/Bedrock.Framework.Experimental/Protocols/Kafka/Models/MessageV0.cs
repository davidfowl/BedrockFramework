#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Services;
using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly ref struct MessageV0
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public readonly byte Magic;
        public readonly byte Attributes;

        public readonly int? KeyLength;
        public readonly byte[]? Key;

        public readonly int? ValueLength;
        public readonly byte[]? Value;

        public MessageV0(
            byte magic,
            byte attributes,
            int? keyLength,
            byte[]? key,
            int? valueLength,
            byte[]? value)
        {
            this.Magic = magic;
            this.Attributes = attributes;

            this.KeyLength = keyLength;
            this.Key = key;

            this.ValueLength = valueLength;
            this.Value = value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.Magic,
                this.Attributes,
                this.KeyLength,
                this.Key,
                this.ValueLength,
                this.Value);
        }

        private const int constantPayloadSize =
            sizeof(int) // crc
            + sizeof(byte) // magic
            + sizeof(byte) // attributes
            + sizeof(int) //  value containing the keyLength
            + sizeof(int); // value containing the valueLength

        public int GetSize()
        {
            return constantPayloadSize
                + (this.KeyLength ?? 0)
                + (this.ValueLength ?? 0);
        }

        public int GetCrc()
        {
            var size = this.GetSize();

            // TODO: Probably a better way to hook up temporary buffers
            // into the common primitives?
            var buffer = ArrayPool<byte>.Shared.Rent(size);
            using var ms = new MemoryStream(buffer, writable: true);
            var pw = PipeWriter.Create(ms);
            var writer = new BufferWriter<IBufferWriter<byte>>(pw);

            writer.WriteByte(this.Magic);
            writer.WriteByte(this.Attributes);

            var key = this.Key;
            var value = this.Value;
            writer.WriteBytes(ref key);
            writer.WriteBytes(ref value);

            writer.Commit();
            pw.Complete();

            var crc = (int)Crc32.Get(buffer);

            ArrayPool<byte>.Shared.Return(buffer);
            
            return crc;
        }

        public int Write(ref BufferWriter<IBufferWriter<byte>> writer)
        {
            var totalSize = this.GetSize();

            writer.WriteInt32BigEndian(totalSize); totalSize += sizeof(int);

            writer.WriteInt32BigEndian(this.GetCrc()); totalSize += sizeof(int);
            writer.WriteByte(this.Magic);
            writer.WriteByte(this.Attributes);

            var key = this.Key;
            var value = this.Value;

            writer.WriteBytes(ref key, this.KeyLength);
            writer.WriteBytes(ref value, this.ValueLength);

            return totalSize;
        }

        public void WritePayload(ref PayloadWriterContext settings)
        {
//            var pw = new PayloadWriter(ref settings)
//                .StartCalculatingSize("message")
//                    .StartCrc32Calculation()
//                        .Write(this.Magic)
//                        .Write(this.Attributes)
//                        .Write(this.Key, this.KeyLength)
//                        .Write(this.Value, this.ValueLength)
//                    .EndCrc32Calculation()
//                .EndSizeCalculation("message");
        }
    }
}
