using Bedrock.Framework.Experimental.Transports.WebSockets;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Bedrock.Framework.Experimental.Tests.Transports
{
    public class WebSocketFrameWriterTests
    {
        private RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();

        [Fact]
        public async Task NoExtendedLengthUnmaskedWorksViaManagedWebSocket()
        {
            var options = new PipeOptions(useSynchronizationContext: false);
            var duplexPipe = DuplexPipe.CreateConnectionPair(options, options);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(duplexPipe.Application.Input, duplexPipe.Application.Output), false, null, TimeSpan.FromSeconds(30));
            var writer = new WebSocketFrameWriter();
            
            var payloadString = "This is a test payload.";
            var payloadBuffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(payloadString));
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, false, (ulong)payloadBuffer.Length, default);

            var protocolWriter = new ProtocolWriter(duplexPipe.Transport.Output);
            await protocolWriter.WriteAsync(writer, new WebSocketWriteFrame(header, payloadBuffer));

            var receiveBuffer = new Memory<byte>(new byte[payloadBuffer.Length]);
            await webSocket.ReceiveAsync(receiveBuffer, default);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(receiveBuffer.ToArray()));
        }

        [Fact]
        public async Task NoExtendedLengthMaskedWorksViaManagedWebSocket()
        {
            var options = new PipeOptions(useSynchronizationContext: false);
            var duplexPipe = DuplexPipe.CreateConnectionPair(options, options);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(duplexPipe.Application.Input, duplexPipe.Application.Output), true, null, TimeSpan.FromSeconds(30));
            var writer = new WebSocketFrameWriter();

            var payloadString = "This is a test payload.";
            var payloadBuffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(payloadString));
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)payloadBuffer.Length, GenerateMaskingKey());

            var protocolWriter = new ProtocolWriter(duplexPipe.Transport.Output);
            await protocolWriter.WriteAsync(writer, new WebSocketWriteFrame(header, payloadBuffer));

            var receiveBuffer = new Memory<byte>(new byte[payloadBuffer.Length]);
            await webSocket.ReceiveAsync(receiveBuffer, default);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(receiveBuffer.ToArray()));
        }

        [Fact]
        public async Task ShortLengthUnmaskedWorksViaManagedWebSocket()
        {
            var options = new PipeOptions(useSynchronizationContext: false);
            var duplexPipe = DuplexPipe.CreateConnectionPair(options, options);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(duplexPipe.Application.Input, duplexPipe.Application.Output), false, null, TimeSpan.FromSeconds(30));
            var writer = new WebSocketFrameWriter();

            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 25));
            var payloadBuffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(payloadString));
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, false, (ulong)payloadBuffer.Length, default);

            var protocolWriter = new ProtocolWriter(duplexPipe.Transport.Output);
            await protocolWriter.WriteAsync(writer, new WebSocketWriteFrame(header, payloadBuffer));

            var receiveBuffer = new Memory<byte>(new byte[payloadBuffer.Length]);
            await webSocket.ReceiveAsync(receiveBuffer, default);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(receiveBuffer.ToArray()));
        }

        [Fact]
        public async Task ShortLengthMaskedWorksViaManagedWebSocket()
        {
            var options = new PipeOptions(useSynchronizationContext: false);
            var duplexPipe = DuplexPipe.CreateConnectionPair(options, options);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(duplexPipe.Application.Input, duplexPipe.Application.Output), true, null, TimeSpan.FromSeconds(30));
            var writer = new WebSocketFrameWriter();

            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 25));
            var payloadBuffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(payloadString));
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)payloadBuffer.Length, GenerateMaskingKey());

            var protocolWriter = new ProtocolWriter(duplexPipe.Transport.Output);
            await protocolWriter.WriteAsync(writer, new WebSocketWriteFrame(header, payloadBuffer));

            var receiveBuffer = new Memory<byte>(new byte[payloadBuffer.Length]);
            await webSocket.ReceiveAsync(receiveBuffer, default);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(receiveBuffer.ToArray()));
        }

        [Fact]
        public async Task ExtendedLengthUnmaskedWorksViaManagedWebSocket()
        {
            var options = new PipeOptions(useSynchronizationContext: false);
            var duplexPipe = DuplexPipe.CreateConnectionPair(options, options);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(duplexPipe.Application.Input, duplexPipe.Application.Output), false, null, TimeSpan.FromSeconds(30));
            var writer = new WebSocketFrameWriter();

            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 2500));
            var payloadBuffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(payloadString));
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, false, (ulong)payloadBuffer.Length, default);

            var protocolWriter = new ProtocolWriter(duplexPipe.Transport.Output);
            var writeTask = protocolWriter.WriteAsync(writer, new WebSocketWriteFrame(header, payloadBuffer));

            var receiveBuffer = new Memory<byte>(new byte[payloadBuffer.Length]);
            await webSocket.ReceiveAsync(receiveBuffer, default);
            await writeTask;

            Assert.Equal(payloadString, Encoding.UTF8.GetString(receiveBuffer.ToArray()));
        }

        [Fact]
        public async Task ExtendedLengthMaskedWorksViaManagedWebSocket()
        {
            var options = new PipeOptions(useSynchronizationContext: false);
            var duplexPipe = DuplexPipe.CreateConnectionPair(options, options);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(duplexPipe.Application.Input, duplexPipe.Application.Output), true, null, TimeSpan.FromSeconds(30));
            var writer = new WebSocketFrameWriter();

            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 2500));
            var payloadBuffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(payloadString));
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)payloadBuffer.Length, GenerateMaskingKey());

            var protocolWriter = new ProtocolWriter(duplexPipe.Transport.Output);
            var writeTask = protocolWriter.WriteAsync(writer, new WebSocketWriteFrame(header, payloadBuffer));

            var receiveBuffer = new Memory<byte>(new byte[payloadBuffer.Length]);
            await webSocket.ReceiveAsync(receiveBuffer, default);
            await writeTask;

            Assert.Equal(payloadString, Encoding.UTF8.GetString(receiveBuffer.ToArray()));
        }

        private int GenerateMaskingKey()
        {
            var maskBytes = new byte[4];
            _rng.GetBytes(maskBytes);

            return BitConverter.ToInt32(maskBytes, 0);
        }
    }
}
