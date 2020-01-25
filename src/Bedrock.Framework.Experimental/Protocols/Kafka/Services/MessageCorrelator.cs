#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests;
using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Services
{
    public class MessageCorrelator : IMessageCorrelator
    {
        // TODO: Move to some form of reflection at startup
        private readonly Dictionary<Type, Type> requestResponseCorrelations
            = new Dictionary<Type, Type>
            {
                // Returns the supported Api calls and versions for associated version.
                { typeof(ApiVersionsRequestV0), typeof(ApiVersionsResponseV0) },

                // Returns broker and/or topic metadata
                { typeof(MetadataRequestV0),    typeof(MetadataResponseV0) },
            };

        private int correlationId = 1;

        private readonly ConcurrentDictionary<int, KafkaRequest> correlations
            = new ConcurrentDictionary<int, KafkaRequest>();

        private readonly IServiceProvider services;
        private readonly ILogger<MessageCorrelator> logger;

        public MessageCorrelator(ILogger<MessageCorrelator> logger, IServiceProvider serviceProvider)
        {
            this.services = serviceProvider;
            this.logger = logger;
        }

        public bool HasCorrelationId(in int correlationId)
            => this.correlations.ContainsKey(correlationId);

        public bool TryAdd(in int correlationId, in KafkaRequest kafkaRequest)
        {
            if (this.correlations.TryAdd(correlationId, kafkaRequest))
            {
                this.logger.LogTrace("Added {CorrelationId} for {KafkaRequest}", correlationId, kafkaRequest.GetType().FullName);

                return true;
            }
            else
            {
                this.logger.LogTrace("Failed to add {CorrelationId} for {KafkaRequest}", correlationId, kafkaRequest.GetType().FullName);

                return false;
            }
        }

        public bool TryCompleteCorrelation(in int correlationId)
        {
            if (!this.correlations.ContainsKey(correlationId))
            {
                throw new ArgumentException($"Unknown correlationId: {correlationId}");
            }

            if (this.correlations.TryRemove(correlationId, out var request))
            {
                request?.Dispose();
                return true;
            }

            return false;
        }

        public KafkaResponse CreatedEmptyCorrelatedResponse(in int correlationId)
        {
            if (!this.correlations.ContainsKey(correlationId))
            {
                throw new ArgumentException($"Unexpected correlationId: {correlationId}", nameof(correlationId));
            }

            return this.CreatedEmptyCorrelatedResponse(this.correlations[correlationId]);
        }

        public KafkaResponse CreatedEmptyCorrelatedResponse(in KafkaRequest request)
        {
            var requestType = request.GetType();
            if (!this.requestResponseCorrelations.ContainsKey(requestType))
            {
                throw new ArgumentException($"Unknown {nameof(KafkaRequest)} type: {requestType.FullName}", nameof(request));
            }

            var responseType = this.requestResponseCorrelations[requestType];

            var response = (KafkaResponse)ActivatorUtilities.CreateInstance(this.services, responseType);

            this.logger.LogDebug("Activated {KafkaResponse} from {KafkaRequest}", response, request);

            return response;
        }

        public int GetCorrelationId(in KafkaRequest request)
        {
            this.correlationId = Interlocked.Increment(ref this.correlationId);

            if (!this.TryAdd(this.correlationId, request))
            {
                throw new InvalidOperationException($"Non-unique correlationId provided in {request.GetType().Name}");
            }

            return this.correlationId;
        }

        private bool disposedValue = false; // To detect redundant calls
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~MessageCorrelator()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
