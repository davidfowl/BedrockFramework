
using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Xunit;

namespace Bedrock.Framework.Experimental
{
    public class ConnectionPoolingTests
    {
        [Fact]
        public async Task TestBasicConnectionPooling()
        {
            var factory = new TestConnectionFactory();
            var pool = new ConnectionPool(factory);
            var endpoint = IPEndPoint.Parse("127.0.0.1:80");
            var connection = await pool.GetConnectionAsync(endpoint);
            Assert.NotNull(connection);

            // No conneciton available, will wait for one to be created.
            var connectionTask = pool.GetConnectionAsync(endpoint);
            Assert.False(connectionTask.IsCompleted);

            // return original to pool
            await connection.DisposeAsync();

            // Await should allow the connection again.
            var connection2 = await connectionTask;
        }


        [Fact]
        public async Task HttpEndPointEqualsWorks()
        {
            var factory = new TestConnectionFactory();
            var pool = new ConnectionPool(factory);
            var endpoint1 = new HttpEndPoint(HttpConnectionKind.Http, "127.0.0.1", 80, "", new Uri("http://localhost"), 1);
            var endpoint2 = new HttpEndPoint(HttpConnectionKind.Http, "127.0.0.1", 80, "", new Uri("http://localhost"), 1);
            var connection = await pool.GetConnectionAsync(endpoint1);
            Assert.NotNull(connection);

            // No conneciton available, will wait for one to be created.
            var connectionTask = pool.GetConnectionAsync(endpoint2);
            Assert.False(connectionTask.IsCompleted);

            // return original to pool
            await connection.DisposeAsync();

            // Await should allow the connection again.
            var connection2 = await connectionTask;
        }

        [Fact]
        public async Task MultipleConnectionForEndPointWorks()
        {
            var factory = new TestConnectionFactory();
            var pool = new ConnectionPool(factory);
            var endpoint = new HttpEndPoint(HttpConnectionKind.Http, "127.0.0.1", 80, "", new Uri("http://localhost"), 2);
            var connection = await pool.GetConnectionAsync(endpoint);
            Assert.NotNull(connection);

            // No conneciton available, will wait for one to be created.
            var connection2 = await pool.GetConnectionAsync(endpoint);

            // return original to pool
            await connection.DisposeAsync();
            await connection2.DisposeAsync();
        }

        private class TestConnectionFactory : IConnectionFactory
        {
            public ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
            {
                var options = new PipeOptions(useSynchronizationContext: false);
                var pair = DuplexPipe.CreateConnectionPair(options, options);
                return new ValueTask<ConnectionContext>(new DefaultConnectionContext(Guid.NewGuid().ToString(), pair.Transport, pair.Application));
            }
        }
    }
}