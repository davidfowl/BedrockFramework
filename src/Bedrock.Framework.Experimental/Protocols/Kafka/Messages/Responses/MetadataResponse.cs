#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses
{
    public class MetadataResponse : KafkaResponse
    {
        public int ThrottleTimeMs { get; private set; }
        public IEnumerable<Broker> Brokers { get; private set; } = Enumerable.Empty<Broker>();
        public string? ClusterId { get; private set; }
        public int ControllerId { get; private set; }
        public IEnumerable<Topic> Topics { get; private set; } = Enumerable.Empty<Topic>();
        public int ClusterAuthorizedOperations { get; private set; }

        public override void ReadResponse(in ReadOnlySequence<byte> response)
        {
            var reader = new SequenceReader<byte>(response);
            this.ThrottleTimeMs = reader.ReadInt32BigEndian();

            this.Brokers = ParseBrokers(ref reader);
            this.ClusterId = reader.ReadNullableString();
            this.ControllerId = reader.ReadInt32BigEndian();

            this.Topics = ParseTopics(ref reader);
            this.ClusterAuthorizedOperations = reader.ReadInt32BigEndian();
        }

        private IEnumerable<Topic> ParseTopics(ref SequenceReader<byte> reader)
        {
            // An array of:
            // topics => error_code name is_internal[partitions] topic_authorized_operations
            //   error_code => INT16
            //   name => STRING
            //   is_internal => BOOLEAN
            //   partitions => error_code partition_index leader_id leader_epoch[replica_nodes] [isr_nodes]
            //     [offline_replicas]
            //     error_code => INT16
            //     partition_index => INT32
            //   leader_id => INT32
            //   leader_epoch => INT32
            //   replica_nodes => INT32
            //   isr_nodes => INT32
            //   offline_replicas => INT32
            //   topic_authorized_operations => INT32

            int arraySize = reader.ReadInt32BigEndian();

            var topics = new List<Topic>(arraySize);
            for (int i = 0; i < arraySize; i++)
            {
                var errorCode = reader.ReadErrorCode();
                var name = reader.ReadString();
                var isInternal = reader.ReadBool();
                var partitions = this.ParsePartitions(ref reader);
                var topicAuth = reader.ReadInt32BigEndian();

                topics.Add(new Topic(
                     errorCode,
                     name,
                     isInternal,
                     partitions,
                     topicAuth));
            }

            return topics;
        }

        private IEnumerable<Partition> ParsePartitions(ref SequenceReader<byte> reader)
        {
            // array of partitions:
            // error_code => INT16
            // partition_index => INT32
            // leader_id => INT32
            // leader_epoch => INT32
            // replica_nodes => INT32
            // isr_nodes => INT32
            // offline_replicas => INT32

            int arraySize = reader.ReadInt32BigEndian();
            var partitions = new List<Partition>(arraySize);

            for (int partIdx = 0; partIdx < arraySize; partIdx++)
            {
                var errorCode = reader.ReadErrorCode();
                ThrowIfError(errorCode);

                var partitionIndex = reader.ReadInt32BigEndian();
                var leaderId = reader.ReadInt32BigEndian();
                var leaderEpoch = reader.ReadInt32BigEndian();
                var replicaNodesCount = reader.ReadInt32BigEndian();
                var replicaNodes = new List<int>(replicaNodesCount);

                for (int replicaNodeIdx = 0; replicaNodeIdx < replicaNodesCount; replicaNodeIdx++)
                {
                    var replicaNode = reader.ReadInt32BigEndian();
                    replicaNodes.Add(replicaNode);
                }

                var isrNodesCount = reader.ReadInt32BigEndian();
                var isrNodes = new List<int>(isrNodesCount);

                for (int isrNodeIdx = 0; isrNodeIdx < isrNodesCount; isrNodeIdx++)
                {
                    var isrNode = reader.ReadInt32BigEndian();
                    isrNodes.Add(isrNode);
                }

                var offlineReplicasCount = reader.ReadInt32BigEndian();
                var offlineReplicas = new List<int>(offlineReplicasCount);

                for (int offlineReplicaIdx = 0; offlineReplicaIdx < offlineReplicasCount; offlineReplicaIdx++)
                {
                    var offReplica = reader.ReadInt32BigEndian();
                    offlineReplicas.Add(offReplica);
                }

                var partition = new Partition(
                        errorCode,
                        partitionIndex,
                        leaderId,
                        leaderEpoch,
                        replicaNodes,
                        isrNodes,
                        offlineReplicas);

                partitions.Add(partition);
            }

            return partitions;
        }

        private IEnumerable<Broker> ParseBrokers(ref SequenceReader<byte> reader)
        {
            // An array of:
            // node_id => INT32
            // host => STRING
            // port => INT32
            // rack => NULLABLE_STRING
            int arraySize = reader.ReadInt32BigEndian();

            var brokers = new List<Broker>(arraySize);

            for (int i = 0; i < arraySize; i++)
            {
                int nodeId = reader.ReadInt32BigEndian();
                string host = reader.ReadString();
                int port = reader.ReadInt32BigEndian();
                string? rack = reader.ReadNullableString();

                brokers.Add(new Broker(
                    nodeId,
                    host,
                    port,
                    rack));
            }

            return brokers;
        }
    }
}

#nullable restore