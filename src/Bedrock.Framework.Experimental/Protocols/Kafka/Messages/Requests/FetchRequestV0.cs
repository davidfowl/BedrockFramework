using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Linq;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests
{
    public class FetchRequestV0 : KafkaRequest
    {
        public FetchRequestV0()
            : base(KafkaApiKeys.Fetch, apiVersion: 0)
        {
        }

        public int ReplicaId { get; set; }
        public int MaxWaitTime { get; set; }
        public int MinBytes { get; set; }
        public FetchTopicV0[] Topics { get; set; } = Array.Empty<FetchTopicV0>();

        private const int constantPayloadSize =
            sizeof(int) // replica_id
            + sizeof(int) // wait
            + sizeof(int) // min bytes
            + sizeof(int); // topic array count

        public override int GetPayloadSize()
        {
            return constantPayloadSize
                + this.Topics.Sum(t => t.GetSize());
        }

        public override void WriteRequest(ref BufferWriter<IBufferWriter<byte>> writer)
        {
            writer.WriteInt32BigEndian(this.ReplicaId);
            writer.WriteInt32BigEndian(this.MaxWaitTime);
            writer.WriteInt32BigEndian(this.MinBytes);
            this.WriteTopics(ref writer, this.Topics);
        }

        private void WriteTopics(ref BufferWriter<IBufferWriter<byte>> writer, in FetchTopicV0[] topics)
        {
            writer.WriteArrayPreamble(topics.Length);
            for (int i = 0; i < topics.Length; i++)
            {
                var topic = topics[i];

                writer.WriteString(topic.Topic);
                this.WriteTopicPartitions(ref writer, topic.Partitions);
            }
        }

        private void WriteTopicPartitions(
            ref BufferWriter<IBufferWriter<byte>> writer,
            FetchTopicPartitionV0[] partitions)
        {
            writer.WriteArrayPreamble(partitions.Length);
            for (int i = 0; i < partitions.Length; i++)
            {
                var partition = partitions[i];
                writer.WriteInt32BigEndian(partition.Partition);
                writer.WriteInt64BigEndian(partition.FetchOffset);
                writer.WriteInt32BigEndian(partition.PartitionMaxBytes);
            }
        }
    }
}

// https://kafka.apache.org/protocol#The_Messages_Fetch
/*
Fetch Request (Version: 0) => replica_id max_wait_time min_bytes [topics] 
  replica_id => INT32
  max_wait_time => INT32
  min_bytes => INT32
  topics => topic [partitions] 
    topic => STRING
    partitions => partition fetch_offset partition_max_bytes 
      partition => INT32
      fetch_offset => INT64
      partition_max_bytes => INT32
*/
