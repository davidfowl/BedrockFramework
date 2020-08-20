using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Bedrock.Framework;
using System.Net.Connections;

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

    public class DnsCachingConnectionFactory : ConnectionFactory
    {
        private readonly TimeSpan _timeout;
        private readonly MemoryCache _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));

        public DnsCachingConnectionFactory(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public ConnectionFactory ConnectionFactory { get; set; }

        public override async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            if (endPoint is DnsEndPoint dnsEndPoint)
            {
                // TODO: Lock etc

                // See if we have an IPEndPoint cached
                var (resolvedEndPoint, resolvedOptions) = _memoryCache.Get<(IPEndPoint, IConnectionProperties)>(dnsEndPoint.Host);

                if (resolvedEndPoint != null)
                {
                    // If it's cached, try to connect
                    try
                    {
                        return await ConnectionFactory.ConnectAsync(resolvedEndPoint, resolvedOptions, cancellationToken);
                    }
                    catch (Exception)
                    {
                        // TODO: Evict from the cache?
                    }
                }

                Connection connection = null;

                // Resolve the DNS entry
                var entry = await Dns.GetHostEntryAsync(dnsEndPoint.Host);


                foreach (var address in entry.AddressList)
                {
                    resolvedEndPoint = new IPEndPoint(address, dnsEndPoint.Port);

                    try
                    {
                        connection = await ConnectionFactory.ConnectAsync(resolvedEndPoint, options);
                        break;
                    }
                    catch (Exception)
                    {

                    }
                }

                if (connection != null)
                {
                    _memoryCache.Set(dnsEndPoint.Host, (resolvedEndPoint, options), new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _timeout
                    });

                    return connection;
                }

                throw new InvalidOperationException($"Unable to resolve {dnsEndPoint.Host} on port {dnsEndPoint.Port}");

            }
            else
            {
                return await ConnectionFactory.ConnectAsync(endPoint, options);
            }
        }
    }
}
