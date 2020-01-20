#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses
{
    public class MetadataResponseV0 : KafkaResponse
    {
        public MetadataBrokerV0[] Brokers { get; private set; } = Array.Empty<MetadataBrokerV0>();
        public MetadataTopicV0[] Topics { get; private set; } = Array.Empty<MetadataTopicV0>();

        public override void FillResponse(in ReadOnlySequence<byte> response)
        {
            var reader = new SequenceReader<byte>(response);

            this.Brokers = this.ParseBrokers(ref reader);
            this.Topics = this.ParseTopics(ref reader);
        }

        private MetadataTopicV0[] ParseTopics(ref SequenceReader<byte> reader)
        {
            int topicCount = reader.ReadInt32BigEndian();

            var topics = new MetadataTopicV0[topicCount];
            for (int i = 0; i < topicCount; i++)
            {
                var errorCode = reader.ReadErrorCode();
                this.ThrowIfError(errorCode);

                var name = reader.ReadString();
                var partitions = this.ParsePartitions(ref reader);

                topics[i] = new MetadataTopicV0(
                     errorCode,
                     name,
                     partitions);
            }

            return topics;
        }

        private MetadataPartitionV0[] ParsePartitions(ref SequenceReader<byte> reader)
        {
            int arraySize = reader.ReadInt32BigEndian();
            var partitions = new MetadataPartitionV0[arraySize];

            for (int partIdx = 0; partIdx < arraySize; partIdx++)
            {
                var errorCode = reader.ReadErrorCode();
                this.ThrowIfError(errorCode);

                var partitionIndex = reader.ReadInt32BigEndian();
                var leaderId = reader.ReadInt32BigEndian();

                var replicaNodesCount = reader.ReadInt32BigEndian();
                var replicaNodes = new int[replicaNodesCount];

                for (int replicaNodeIdx = 0; replicaNodeIdx < replicaNodesCount; replicaNodeIdx++)
                {
                    var replicaNode = reader.ReadInt32BigEndian();
                    replicaNodes[replicaNodeIdx] = replicaNode;
                }

                var isrNodesCount = reader.ReadInt32BigEndian();
                var isrNodes = new int[isrNodesCount];

                for (int isrNodeIdx = 0; isrNodeIdx < isrNodesCount; isrNodeIdx++)
                {
                    var isrNode = reader.ReadInt32BigEndian();
                    isrNodes[isrNodeIdx] = isrNode;
                }

                var partition = new MetadataPartitionV0(
                    errorCode,
                    partitionIndex,
                    leaderId,
                    replicaNodes,
                    isrNodes);

                partitions[partIdx] = partition;
            }

            return partitions;
        }

        private MetadataBrokerV0[] ParseBrokers(ref SequenceReader<byte> reader)
        {
            int arraySize = reader.ReadInt32BigEndian();

            var brokers = new MetadataBrokerV0[arraySize];

            for (int i = 0; i < arraySize; i++)
            {
                int nodeId = reader.ReadInt32BigEndian();
                string host = reader.ReadString();
                int port = reader.ReadInt32BigEndian();

                brokers[i] = new MetadataBrokerV0(
                    nodeId,
                    host,
                    port);
            }

            return brokers;
        }
    }
}

/*
Metadata Response (Version: 0) => [brokers] [topics] 
  brokers => node_id host port 
    node_id => INT32
    host => STRING
    port => INT32
  topics => error_code name [partitions] 
    error_code => INT16
    name => STRING
    partitions => error_code partition_index leader_id [replica_nodes] [isr_nodes] 
      error_code => INT16
      partition_index => INT32
      leader_id => INT32
      replica_nodes => INT32
      isr_nodes => INT32
 */
