using System;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bedrock.Framework.Tests
{
    public class ServerTests
    {
        [Fact]
        public async Task StartSocketWithServer()
        {
            var (server, testResult) = await StartServer();

            var expected = "Hello hello!";
            await StartClient(5000, expected);

            await Task.WhenAny(
                testResult.Completion.Task,
                Task.Delay(TimeSpan.FromSeconds(5))
            );

            Assert.True(testResult.Completion.Task.IsCompleted);
            Assert.Equal(expected, testResult.Completion.Task.Result);

            await server.StopAsync();
        }

        [Fact]
        public async Task StartSocketAfterServer()
        {
            var (server, testResult) = await StartServer();

            var endpoint = new IPEndPoint(IPAddress.Loopback, 5001);
            await server.AddSocketListenerAsync(endpoint,
                builder => builder.UseConnectionHandler<TestApplication>());

            const string expected = "Hello hello!";
            await StartClient(5001, expected);

            await Task.WhenAny(
                testResult.Completion.Task,
                Task.Delay(TimeSpan.FromSeconds(5))
            );

            Assert.True(testResult.Completion.Task.IsCompleted);
            Assert.Equal(expected, testResult.Completion.Task.Result);

            await server.StopAsync();
        }

        [Fact]
        public async Task StopSocketBeforeServer()
        {
            var (server, _) = await StartServer();

            Assert.NotNull(server.EndPoints.SingleOrDefault(x => x is IPEndPoint endpoint && endpoint.Address.Equals(IPAddress.Loopback) && endpoint.Port == 5000));
            var endpointToRemove = new IPEndPoint(IPAddress.Loopback, 5000);
            await server.RemoveSocketListener(endpointToRemove);
            Assert.Null(server.EndPoints.SingleOrDefault(x => x is IPEndPoint endpoint && endpoint.Address.Equals(IPAddress.Loopback) && endpoint.Port == 5000));

            await server.StopAsync();
        }

        private static async Task StartClient(int port, string input)
        {
            // Setup Client
            var clientServiceProvider = new ServiceCollection().BuildServiceProvider();

            var client = new ClientBuilder(clientServiceProvider)
                .UseSockets()
                .Build();

            var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port));

            var reads = new MemoryStream(Encoding.UTF8.GetBytes(input)).CopyToAsync(connection.Transport.Output);
            await reads;
        }

        private static async Task<(Server server, TestResult testResult)> StartServer()
        {
            var services = new ServiceCollection().AddScoped<TestResult>();
            var serviceProvider = services.BuildServiceProvider();

            var server = new ServerBuilder(serviceProvider)
                .UseSockets(socketsServerBuilder =>
                    socketsServerBuilder.ListenLocalhost(5000, builder =>
                        builder.UseConnectionHandler<TestApplication>()))
                .Build();

            await server.StartAsync();

            var testResult = serviceProvider.GetRequiredService<TestResult>();
            return (server, testResult);
        }
    }

    internal class TestResult
    {
        public TaskCompletionSource<string> Completion { get; } = new TaskCompletionSource<string>();
    }

    internal class TestApplication : ConnectionHandler
    {
        private readonly TestResult _testResult;

        public TestApplication(TestResult testResult)
        {
            _testResult = testResult;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            try
            {
                var requestBodyInBytes = await connection.Transport.Input.ReadAsync();
                connection.Transport.Input.AdvanceTo(requestBodyInBytes.Buffer.Start, requestBodyInBytes.Buffer.End);
                var input = Encoding.UTF8.GetString(requestBodyInBytes.Buffer.FirstSpan);
                _testResult.Completion.SetResult(input);
            }
            catch
            {
                // Connection closed
            }
        }
    }
}