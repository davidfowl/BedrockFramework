using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Linq;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests
{
    public class ProduceRequestV0 : KafkaRequest
    {
        private int? keyLength;
        private int? valueLength;

        public ProduceRequestV0(in ProducePayload payload)
            : base(KafkaApiKeys.Produce, apiVersion: 0)
        {
            this.Topic = payload.TopicPartition;

            if (payload.Key != null)
            {
                this.keyLength = payload.Key.Length;
                this.Key = ArrayPool<byte>.Shared.Rent(this.keyLength.Value);
                payload.Key.TryCopyTo(this.Key);
            }

            if (payload.Value != null)
            {
                this.valueLength = payload.Value.Length;
                this.Value = ArrayPool<byte>.Shared.Rent(this.valueLength.Value);
                payload.Value.TryCopyTo(this.Value);
            }
        }

        public short Acks { get; set; } = -1;

        public int Timeout { get; set; } = 1500;

        public TopicPartition Topic { get; set; }

        public byte[] Key { get; set; }
        public byte[] Value { get; set; }

        private const int constantPayloadsize =
            sizeof(short) // acks
            + sizeof(int) // timeout
            + sizeof(int) // topics array count
            + sizeof(short) // topic array name length
            + sizeof(int) // single partition index
            + sizeof(long) // offset
            + sizeof(int) // message set size
            + sizeof(int) // message size
            + sizeof(int) // key length size
            + sizeof(int) // value length size
            + 14;

        public override int GetPayloadSize()
        {
            return constantPayloadsize
                + this.Topic.TopicName.Length
                + (this.keyLength ?? 0)
                + (this.valueLength ?? 0);
        }

        public override void WriteRequest(ref BufferWriter<IBufferWriter<byte>> writer)
        {
            writer.WriteInt16BigEndian(this.Acks);
            writer.WriteInt32BigEndian(this.Timeout);

            // Topic array - no batching for now, single topic
            writer.WriteArrayPreamble(1);

            // write topic string
            writer.WriteString(this.Topic.TopicName);

            // partition array - single partition
            writer.WriteArrayPreamble(1);

            //write partition id
            writer.WriteInt32BigEndian(this.Topic.Partition.Index);
            this.WriteMessageSet(ref writer);
        }

        private void WriteMessageSet(ref BufferWriter<IBufferWriter<byte>> writer)
        {
            // TODO: if I could get the buffer's current location, I should be able to
            // store where I am, accumulate the size, then write the size in the spot at
            // the beginning? Won't work for all protocols.

            var temporaryBuffer = ArrayPool<byte>.Shared.Rent(writer.Span.Length);

            using var ms = new MemoryStream(temporaryBuffer);
            var pw = PipeWriter.Create(ms);
            var temporaryBufferWriter = new BufferWriter<IBufferWriter<byte>>(pw);

            var messageSetSize = this.WriteMessages(ref temporaryBufferWriter);

            temporaryBufferWriter.Commit();
            pw.Complete();

            writer.WriteInt32BigEndian(messageSetSize);

            var tempBufferSpan = new ReadOnlySpan<byte>(temporaryBuffer, 0, messageSetSize);
            writer.Write(tempBufferSpan);

            ArrayPool<byte>.Shared.Return(temporaryBuffer);
        }

        private int WriteMessages(ref BufferWriter<IBufferWriter<byte>> writer)
        {
            var totalSize = 0;
            long offset = -1;

            // Just doing a message at a time right now, but laying some things out
            // with multiple records in mind.

            var message = new MessageV0(
                magic: 0,
                attributes: 0,
                this.keyLength,
                this.Key,
                this.valueLength,
                this.Value);

            totalSize += this.WriteMessage(ref writer, ref message, ref offset);

            return totalSize;
        }

        private int WriteMessage(ref BufferWriter<IBufferWriter<byte>> writer, ref MessageV0 message, ref long offset)
        {
            // Offset - when compression comes in, offset will need to be adapted.
            writer.WriteInt64BigEndian(offset);

            return message.Write(ref writer)
                + sizeof(long);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (this.Key != null)
            {
                ArrayPool<byte>.Shared.Return(this.Key);
                this.Key = null;
            }

            if (this.Value != null)
            {
                ArrayPool<byte>.Shared.Return(this.Value);
            }
        }
    }
}

// https://kafka.apache.org/protocol#The_Messages_Produce
/*
 Produce Request (Version: 0) => acks timeout [topic_data] 
  acks => INT16
  timeout => INT32
  topic_data => topic [data] 
    topic => STRING
    data => partition record_set 
      partition => INT32
      record_set => RECORDS
*/
