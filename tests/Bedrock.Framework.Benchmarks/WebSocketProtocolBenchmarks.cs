using Bedrock.Framework.Protocols.WebSockets;
using BenchmarkDotNet.Attributes;
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
    public class WebSocketProtocolBenchmarks
    {
        private WebSocket _webSocket;

        private WebSocketProtocol _webSocketProtocol;

        private DefaultConnectionContext _connectionContext;

        private MemoryStream _stream;

        private byte[] _message;

        private ArraySegment<byte> _arrayBuffer;

        private ReadOnlyMemory<byte> _romBuffer;

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
            var writer = new WebSocketFrameWriter();
            var pipe = new Pipe();

            _message = new byte[4000];

            var header = WebSocketHeader.CreateMasked(true, WebSocketOpcode.Binary, 4000);
            writer.WriteMessage(new WebSocketWriteFrame(header, new ReadOnlySequence<byte>(_message)), pipe.Writer);

            await pipe.Writer.FlushAsync();

            var result = await pipe.Reader.ReadAsync();
            _message = result.Buffer.ToArray();

            var dummyReader = new DummyPipeReader { Result = new ReadResult(new ReadOnlySequence<byte>(_message), false, false) };
            var dummyDuplexPipe = new DummyDuplexPipe { DummyReader = dummyReader };

            _connectionContext = new DefaultConnectionContext { Transport = dummyDuplexPipe };
            _stream = new MemoryStream(_message);

            _webSocket = WebSocket.CreateFromStream(_stream, true, null, TimeSpan.FromSeconds(30));
            _webSocketProtocol = new WebSocketProtocol(_connectionContext, WebSocketProtocolType.Server);

            _arrayBuffer = new ArraySegment<byte>(new byte[10000]);
            _romBuffer = new ReadOnlyMemory<byte>(_message);
        }

        [Benchmark(Baseline = true)]
        public async ValueTask WebSocketReadMasked()
        {
            _stream.Seek(0, SeekOrigin.Begin);

            var endOfMessage = false;
            while (!endOfMessage)
            {
                var result = await _webSocket.ReceiveAsync(_arrayBuffer, CancellationToken.None);
                endOfMessage = result.EndOfMessage;
            }
        }

        [Benchmark]
        public async ValueTask WebSocketProtocolReadMasked()
        {
            var message = await _webSocketProtocol.ReadAsync();
            var data = await message.Reader.ReadAsync();
            message.Reader.AdvanceTo(data.Data.End);
        }
    }
}
