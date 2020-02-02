using Bedrock.Framework.Protocols.WebSockets;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.AspNetCore.Connections;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Benchmarks
{
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class WebSocketProtocolBenchmarks
    {
        private WebSocket _webSocketServer;

        private WebSocket _webSocketClient;

        private WebSocketProtocol _webSocketProtocolServer;

        private WebSocketProtocol _webSocketProtocolClient;

        private DefaultConnectionContext _serverConnectionContext;

        private DefaultConnectionContext _clientConnectionContext;

        private MemoryStream _serverStream;

        private MemoryStream _clientStream;

        private ArraySegment<byte> _arrayBuffer;

        private class DummyPipeReader : PipeReader
        {
            public ReadResult Result { get; set; }

            public override void AdvanceTo(SequencePosition consumed) { }

            public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) { }

            public override void CancelPendingRead() { }

            public override void Complete(Exception exception = null) { }

            public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
            {
                return new ValueTask<ReadResult>(Result);
            }

            public override bool TryRead(out ReadResult result)
            {
                result = Result;
                return true;
            }
        }

        private class DummyDuplexPipe : IDuplexPipe
        {
            public PipeReader Input => DummyReader;

            public PipeWriter Output => throw new NotImplementedException();

            public DummyPipeReader DummyReader { get; set; }
        }

        [GlobalSetup]
        public async ValueTask Setup()
        {
            var serverMessage = await GetMessageBytes(true, 4000);
            var clientMessage = await GetMessageBytes(false, 4000);

            (_serverConnectionContext, _serverStream) = CreateContextAndStream(serverMessage);
            (_clientConnectionContext, _clientStream) = CreateContextAndStream(clientMessage);

            _webSocketServer = WebSocket.CreateFromStream(_serverStream, true, null, TimeSpan.FromSeconds(30));
            _webSocketProtocolServer = new WebSocketProtocol(_serverConnectionContext, WebSocketProtocolType.Server);

            _webSocketClient = WebSocket.CreateFromStream(_clientStream, false, null, TimeSpan.FromSeconds(30));
            _webSocketProtocolClient = new WebSocketProtocol(_clientConnectionContext, WebSocketProtocolType.Server);

            _arrayBuffer = new ArraySegment<byte>(new byte[10000]);
        }

        private async ValueTask<byte[]> GetMessageBytes(bool isMasked, long size)
        {
            var writer = new WebSocketFrameWriter();
            var pipe = new Pipe();

            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, isMasked, (ulong)size, isMasked ? WebSocketHeader.GenerateMaskingKey() : default);
            writer.WriteMessage(new WebSocketWriteFrame(header, new ReadOnlySequence<byte>(new byte[4000])), pipe.Writer);

            await pipe.Writer.FlushAsync();

            var result = await pipe.Reader.ReadAsync();
            return result.Buffer.ToArray();
        }

        private (DefaultConnectionContext context, MemoryStream stream) CreateContextAndStream(byte[] message)
        {
            var reader = new DummyPipeReader { Result = new ReadResult(new ReadOnlySequence<byte>(message), false, false) };
            var duplexPipe = new DummyDuplexPipe { DummyReader = reader };

            var stream = new MemoryStream(message);
            var context = new DefaultConnectionContext { Transport = duplexPipe };

            return (context, stream);
        }

        [BenchmarkCategory("Masked"), Benchmark(Baseline = true)]
        public async ValueTask WebSocketRead()
        {
            _clientStream.Seek(0, SeekOrigin.Begin);

            var endOfMessage = false;
            while (!endOfMessage)
            {
                var result = await _webSocketClient.ReceiveAsync(_arrayBuffer, CancellationToken.None);
                endOfMessage = result.EndOfMessage;
            }
        }

        [BenchmarkCategory("Unmasked"), Benchmark(Baseline = true)]
        public async ValueTask WebSocketReadMasked()
        {
            _serverStream.Seek(0, SeekOrigin.Begin);

            var endOfMessage = false;
            while (!endOfMessage)
            {
                var result = await _webSocketServer.ReceiveAsync(_arrayBuffer, CancellationToken.None);
                endOfMessage = result.EndOfMessage;
            }
        }

        [BenchmarkCategory("Masked"), Benchmark]
        public async ValueTask WebSocketProtocolRead()
        {
            var message = await _webSocketProtocolClient.ReadAsync();
            var data = await message.Reader.ReadAsync();
            message.Reader.AdvanceTo(data.Data.End);
        }

        [BenchmarkCategory("Unmasked"), Benchmark]
        public async ValueTask WebSocketProtocolReadMasked()
        {
            var message = await _webSocketProtocolServer.ReadAsync();
            var data = await message.Reader.ReadAsync();
            message.Reader.AdvanceTo(data.Data.End);
        }
    }
}
