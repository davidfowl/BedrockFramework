namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests
{
    public readonly struct FetchTopic
    {
        //       topics => topic[partitions]
        //         topic => STRING
        //   partitions => partition current_leader_epoch fetch_offset log_start_offset partition_max_bytes
        //     partition => INT32
        //     current_leader_epoch => INT32
        //     fetch_offset => INT64
        //     log_start_offset => INT64
        //     partition_max_bytes => INT32
    }
}