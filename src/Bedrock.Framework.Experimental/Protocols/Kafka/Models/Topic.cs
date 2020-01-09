using System.Collections.Generic;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Models
{
    public readonly struct Topic
    {
        public readonly KafkaErrorCode ErrorCode;
        public readonly string Name;
        public readonly bool IsInternal;
        public readonly IEnumerable<Partition> Partitions;
        public readonly int TopicAuthorizedOperations;

        public Topic(
            KafkaErrorCode error,
            string name,
            bool isInternal,
            IEnumerable<Partition> partitions,
            int topicAuthorizedOptions)
        {
            this.ErrorCode = error;
            this.Name = name;
            this.IsInternal = isInternal;
            this.Partitions = partitions;
            this.TopicAuthorizedOperations = topicAuthorizedOptions;
        }
    }
}
