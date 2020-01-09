using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests
{
    public class FetchRequest : KafkaRequest
    {
        public FetchRequest()
            : base(apiKey: KafkaApiKeys.Fetch, apiVersion: 11)
        {
        }

        public int ReplicaId { get; set; }
        public int MaxWaitTime { get; set; }
        public int MinBytes { get; set; }
        public int MaxBytes { get; set; }
        public byte IsolationLevel { get; set; }
        public int SessionId { get; set; }
        public int SessionEpoch { get; set; }
        public List<FetchTopic> Topics { get; set; } = new List<FetchTopic>();
        public List<FetchForgottenTopic> ForgottenTopics { get; set; } = new List<FetchForgottenTopic>();
        public string RackId { get; set; }

        public override void WriteRequest(ref BufferWriter<IBufferWriter<byte>> output)
        {
            var mw = new PayloadWriter(output, shouldCalculateSizeBeforeWriting: true, isBigEndian: true)
                .WriteCalculatedSize()
                .Write(this.ReplicaId)
                .Write(this.MaxWaitTime)
                .Write(this.MinBytes)
                .Write(this.MaxBytes);

            if (mw.TryWritePayload(out var payload))
            {
                Debug.Assert(payload.Length == 20);
            }
            else
            {

            }
        }
    }
}

// https://kafka.apache.org/protocol#The_Messages_Fetch
/*
Fetch Request (Version: 11) => replica_id max_wait_time min_bytes max_bytes isolation_level session_id session_epoch [topics] [forgotten_topics_data] rack_id 
  replica_id => INT32
  max_wait_time => INT32
  min_bytes => INT32
  max_bytes => INT32
  isolation_level => INT8
  session_id => INT32
  session_epoch => INT32
  topics => topic [partitions] 
    topic => STRING
    partitions => partition current_leader_epoch fetch_offset log_start_offset partition_max_bytes 
      partition => INT32
      current_leader_epoch => INT32
      fetch_offset => INT64
      log_start_offset => INT64
      partition_max_bytes => INT32
  forgotten_topics_data => topic [partitions] 
    topic => STRING
    partitions => INT32
  rack_id => STRING
*/
