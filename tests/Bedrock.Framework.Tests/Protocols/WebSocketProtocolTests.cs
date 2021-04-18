using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Protocols.WebSockets;
using Bedrock.Framework.Tests.Infrastructure;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Bedrock.Framework.Tests.Protocols
{
    public class WebSocketProtocolTests
    {
        private byte[] _buffer = new byte[4096];

        [Fact]
        public async Task SingleMessageWorks()
        {
            var context = new InMemoryConnectionContext(new PipeOptions(useSynchronizationContext: false));
            var protocol = new WebSocketProtocol(context, WebSocketProtocolType.Server);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(context.Application.Input, context.Application.Output), false, null, TimeSpan.FromSeconds(30));
            var payloadString = "This is a test payload.";
            await webSocket.SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payloadString)), WebSocketMessageType.Binary, true, default);

            var message = await protocol.ReadAsync();        
            var result = await message.Reader.ReadAsync();
            Assert.Equal(payloadString, Encoding.UTF8.GetString(result.Data.ToArray()));
        }

        [Fact]
        public async Task MultipleMessagesWorks()
        {
            var context = new InMemoryConnectionContext(new PipeOptions(useSynchronizationContext: false));
            var protocol = new WebSocketProtocol(context, WebSocketProtocolType.Server);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(context.Application.Input, context.Application.Output), false, null, TimeSpan.FromSeconds(30));
            var payloadString = "This is a test payload.";

            for (var i = 0; i < 5; i++)
            {
                await webSocket.SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payloadString)), WebSocketMessageType.Binary, true, default);
            }

            for (var i = 0; i < 5; i++)
            {
                var message = await protocol.ReadAsync();
                var result = await message.Reader.ReadAsync();

                Assert.Equal(payloadString, Encoding.UTF8.GetString(result.Data.ToArray()));
                message.Reader.AdvanceTo(result.Data.End);          
            }
        }

        [Fact]
        public async Task MessageWithMultipleFramesWorks()
        {
            var context = new InMemoryConnectionContext(new PipeOptions(useSynchronizationContext: false));
            var protocol = new WebSocketProtocol(context, WebSocketProtocolType.Server);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(context.Application.Input, context.Application.Output), false, null, TimeSpan.FromSeconds(30));
            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 2500));

            var payloadBytes = Encoding.UTF8.GetBytes(payloadString).Split(8);
            foreach(var segment in payloadBytes.SkipLast(1))
            {
                await webSocket.SendAsync(new ReadOnlyMemory<byte>(segment), WebSocketMessageType.Binary, false, default);
            }

            await webSocket.SendAsync(new ReadOnlyMemory<byte>(payloadBytes.Last()), WebSocketMessageType.Binary, true, default);

            var message = await protocol.ReadAsync();
            var buffer = new ArrayBufferWriter<byte>();
            while(true)
            {
                var result = await message.Reader.ReadAsync();
                foreach(var segment in result.Data)
                {
                    buffer.Write(segment.Span);
                }

                message.Reader.AdvanceTo(result.Data.End);

                if(result.IsEndOfMessage)
                {
                    break;
                }
            }    

            Assert.Equal(payloadString, Encoding.UTF8.GetString(buffer.WrittenSpan));
        }

        [Fact]
        public async Task WriteSingleMessageWorks()
        {
            var context = new InMemoryConnectionContext(new PipeOptions(useSynchronizationContext: false));
            var protocol = new WebSocketProtocol(context, WebSocketProtocolType.Server);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(context.Application.Input, context.Application.Output), false, null, TimeSpan.FromSeconds(30));
            var payloadString = "This is a test payload.";
            await protocol.WriteSingleFrameMessageAsync(new ReadOnlySequence<byte>(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payloadString))), false, default);

            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(_buffer), default);         
            Assert.Equal(payloadString, Encoding.UTF8.GetString(_buffer, 0, result.Count));
        }

        [Fact]
        public async Task WriteMultipleFramesWorks()
        {
            var context = new InMemoryConnectionContext(new PipeOptions(useSynchronizationContext: false));
            var protocol = new WebSocketProtocol(context, WebSocketProtocolType.Server);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(context.Application.Input, context.Application.Output), false, null, TimeSpan.FromSeconds(30));
            var payloadString = "This is a test payload.";
            var writer = await protocol.StartMessageAsync(new ReadOnlySequence<byte>(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payloadString))), false, default);
            await writer.EndMessageAsync(new ReadOnlySequence<byte>(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payloadString))));

            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(_buffer, 0, _buffer.Length), default);
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(_buffer, result.Count, _buffer.Length - result.Count), default);

            Assert.Equal($"{payloadString}{payloadString}", Encoding.UTF8.GetString(_buffer, 0, result.Count * 2));
        }
    }
}
