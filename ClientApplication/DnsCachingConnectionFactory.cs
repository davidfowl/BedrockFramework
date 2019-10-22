using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Bedrock.Framework;

namespace ClientApplication
{
    public static class DnsCachingConnectionExtensions
    {
        public static ClientBuilder UseDnsCaching(this ClientBuilder clientBuilder, TimeSpan timeout)
        {
            return clientBuilder.Use(previous => new DnsCachingConnectionFactory(timeout)
            {
                ConnectionFactory = previous
            });
        }
    }

    public class DnsCachingConnectionFactory : IConnectionFactory
    {
        private readonly TimeSpan _timeout;
        private readonly MemoryCache _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));

        public DnsCachingConnectionFactory(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public IConnectionFactory ConnectionFactory { get; set; }

        public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            if (endpoint is DnsEndPoint dnsEndPoint)
            {
                // TODO: Lock etc

                // See if we have an IPEndPoint cached
                var resolvedEndPoint = _memoryCache.Get<IPEndPoint>(dnsEndPoint.Host);

                if (resolvedEndPoint != null)
                {
                    // If it's cached, try to connect
                    try
                    {
                        return await ConnectionFactory.ConnectAsync(resolvedEndPoint);
                    }
                    catch (Exception)
                    {
                        // TODO: Evict from the cache?
                    }
                }

                ConnectionContext connectionContext = null;

                // Resolve the DNS entry
                var entry = await Dns.GetHostEntryAsync(dnsEndPoint.Host);


                foreach (var address in entry.AddressList)
                {
                    resolvedEndPoint = new IPEndPoint(address, dnsEndPoint.Port);

                    try
                    {
                        connectionContext = await ConnectionFactory.ConnectAsync(resolvedEndPoint);
                        break;
                    }
                    catch (Exception)
                    {

                    }
                }

                if (connectionContext != null)
                {
                    _memoryCache.Set(dnsEndPoint.Host, resolvedEndPoint, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _timeout
                    });

                    return connectionContext;
                }

                throw new InvalidOperationException($"Unable to resolve {dnsEndPoint.Host} on port {dnsEndPoint.Port}");

            }
            else
            {
                return await ConnectionFactory.ConnectAsync(endpoint);
            }
        }
    }
}
