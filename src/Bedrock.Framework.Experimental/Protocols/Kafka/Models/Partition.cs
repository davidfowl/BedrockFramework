#nullable enable

using System.Collections.Generic;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct Partition
    {
        public readonly KafkaErrorCode ErrorCode;
        public readonly int PartitionIndex;
        public readonly int LeaderId;
        public readonly int LeaderEpoch;
        public readonly IEnumerable<int> ReplicaNodes;
        public readonly IEnumerable<int> IsrNodes;
        public readonly IEnumerable<int> OfflineReplicas;

        public Partition(
            KafkaErrorCode errorCode,
            int partitionIndex,
            int leaderId,
            int leaderEpoch,
            IEnumerable<int> replicaNodes,
            IEnumerable<int> isrNodes,
            IEnumerable<int> offlineReplicas)
        {
            this.ErrorCode = errorCode;
            this.PartitionIndex = partitionIndex;
            this.LeaderId = leaderId;
            this.LeaderEpoch = leaderEpoch;
            this.ReplicaNodes = replicaNodes;
            this.IsrNodes = isrNodes;
            this.OfflineReplicas = offlineReplicas;
        }
    }
}

#nullable restore