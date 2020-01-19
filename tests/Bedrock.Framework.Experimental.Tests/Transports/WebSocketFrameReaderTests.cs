using Bedrock.Framework.Experimental.Transports.WebSockets;
using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Bedrock.Framework.Experimental.Tests.Transports
{
    public class WebSocketFrameReaderTests
    {
        [Fact]
        public void SequenceLessThanMinimumHeaderLengthReturnsFalse()
        {
            var reader = new WebSocketFrameReader();
            var sequence = new ReadOnlySequence<byte>(new byte[0]);
            SequencePosition pos = default;

            var success = reader.TryParseMessage(sequence, ref pos, ref pos, out var message);
            Assert.False(success);
        }

        [Fact]
        public void SequenceLessThanMinimumPlusMaskReturnsFalse()
        {
            var reader = new WebSocketFrameReader();
            var headerBytes = GetHeaderBytes(new WebSocketHeader(true, default, true, 64, GetMaskingKey()));

            var sequence = new ReadOnlySequence<byte>(headerBytes.Slice(0, 2));
            SequencePosition pos = default;

            var success = reader.TryParseMessage(sequence, ref pos, ref pos, out var message);
            Assert.False(success);
        }

        [Fact]
        public void SequenceLessThanMinimumPlusShortExtendedLengthReturnsFalse()
        {
            var reader = new WebSocketFrameReader();
            var headerBytes = GetHeaderBytes(new WebSocketHeader(true, default, true, 126, GetMaskingKey()));

            var sequence = new ReadOnlySequence<byte>(headerBytes.Slice(0, 2));
            SequencePosition pos = default;

            var success = reader.TryParseMessage(sequence, ref pos, ref pos, out var message);
            Assert.False(success);
        }

        [Fact]
        public void SequenceLessThanMinimumPlusLongExtendedLengthReturnsFalse()
        {
            var reader = new WebSocketFrameReader();
            var headerBytes = GetHeaderBytes(new WebSocketHeader(true, default, true, ushort.MaxValue + 1, GetMaskingKey()));

            var sequence = new ReadOnlySequence<byte>(headerBytes.Slice(0, 4));
            SequencePosition pos = default;

            var success = reader.TryParseMessage(sequence, ref pos, ref pos, out var message);
            Assert.False(success);
        }

        [Fact]
        public void FinUnmaskedNoExtendedLengthHeaderWorks()
        {
            var reader = new WebSocketFrameReader();
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, false, 64, 0);
            var headerBytes = GetHeaderBytes(header);

            var sequence = new ReadOnlySequence<byte>(headerBytes);
            SequencePosition pos = default;

            var success = reader.TryParseMessage(sequence, ref pos, ref pos, out var message);

            Assert.True(success);
            Assert.Equal(sequence.GetPosition(headerBytes.Length), pos);
            Assert.Equal(header, message.Header);
        }

        [Fact]
        public void FinUnmaskedShortLengthHeaderWorks()
        {
            var reader = new WebSocketFrameReader();
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, false, 126, 0);
            var headerBytes = GetHeaderBytes(header);

            var sequence = new ReadOnlySequence<byte>(headerBytes);
            SequencePosition pos = default;

            var success = reader.TryParseMessage(sequence, ref pos, ref pos, out var message);

            Assert.True(success);
            Assert.Equal(sequence.GetPosition(headerBytes.Length), pos);
            Assert.Equal(header, message.Header);
        }

        [Fact]
        public void FinUnmaskedExtendedLengthHeaderWorks()
        {
            var reader = new WebSocketFrameReader();
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, false, ushort.MaxValue + 1, 0);
            var headerBytes = GetHeaderBytes(header);

            var sequence = new ReadOnlySequence<byte>(headerBytes);
            SequencePosition pos = default;

            var success = reader.TryParseMessage(sequence, ref pos, ref pos, out var message);

            Assert.True(success);
            Assert.Equal(sequence.GetPosition(headerBytes.Length), pos);
            Assert.Equal(header, message.Header);
        }

        [Fact]
        public void FinMaskedNoExtendedLengthHeaderWorks()
        {
            var reader = new WebSocketFrameReader();
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, true, 64 + 1, GetMaskingKey());
            var headerBytes = GetHeaderBytes(header);

            var sequence = new ReadOnlySequence<byte>(headerBytes);
            SequencePosition pos = default;

            var success = reader.TryParseMessage(sequence, ref pos, ref pos, out var message);

            Assert.True(success);
            Assert.Equal(sequence.GetPosition(headerBytes.Length), pos);
            Assert.Equal(header, message.Header);
        }

        [Fact]
        public void FinMaskedShortLengthHeaderWorks()
        {
            var reader = new WebSocketFrameReader();
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, true, 256, GetMaskingKey());
            var headerBytes = GetHeaderBytes(header);

            var sequence = new ReadOnlySequence<byte>(headerBytes);
            SequencePosition pos = default;

            var success = reader.TryParseMessage(sequence, ref pos, ref pos, out var message);

            Assert.True(success);
            Assert.Equal(sequence.GetPosition(headerBytes.Length), pos);
            Assert.Equal(header, message.Header);
        }

        [Fact]
        public void FinMaskedExtendedLengthHeaderWorks()
        {
            var reader = new WebSocketFrameReader();
            var header = new WebSocketHeader(true, WebSocketOpcode.Binary, true, ushort.MaxValue + 1, GetMaskingKey());
            var headerBytes = GetHeaderBytes(header);

            var sequence = new ReadOnlySequence<byte>(headerBytes);
            SequencePosition pos = default;

            var success = reader.TryParseMessage(sequence, ref pos, ref pos, out var message);

            Assert.True(success);
            Assert.Equal(sequence.GetPosition(headerBytes.Length), pos);
            Assert.Equal(header, message.Header);
        }

        [Fact]
        public async Task FinUnmaskedNoExtendedLengthViaManagedWebsocketWorks()
        {
            var pipe = DuplexPipe.CreateConnectionPair(PipeOptions.Default, PipeOptions.Default);
            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(pipe.Transport.Input, pipe.Transport.Output), true, null, TimeSpan.FromSeconds(30));

            await webSocket.SendAsync(new ReadOnlyMemory<byte>(new byte[16]), WebSocketMessageType.Binary, true, CancellationToken.None);
            var result = await pipe.Application.Input.ReadAsync();

            var reader = new WebSocketFrameReader();
            SequencePosition pos = default;
            reader.TryParseMessage(result.Buffer, ref pos, ref pos, out var frame);

            Assert.Equal(new WebSocketHeader(true, WebSocketOpcode.Binary, false, 16, 0), frame.Header);
        }

        [Fact]
        public async Task FinMaskedNoExtendedLengthViaManagedWebsocketWorks()
        {
            var pipe = DuplexPipe.CreateConnectionPair(PipeOptions.Default, PipeOptions.Default);
            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(pipe.Transport.Input, pipe.Transport.Output), false, null, TimeSpan.FromSeconds(30));

            await webSocket.SendAsync(new ReadOnlyMemory<byte>(new byte[16]), WebSocketMessageType.Binary, true, CancellationToken.None);
            var result = await pipe.Application.Input.ReadAsync();

            var reader = new WebSocketFrameReader();
            SequencePosition pos = default;
            reader.TryParseMessage(result.Buffer, ref pos, ref pos, out var frame);

            var maskingKey = ReadMaskingKey(result.Buffer, 2);
            Assert.Equal(new WebSocketHeader(true, WebSocketOpcode.Binary, true, 16, maskingKey), frame.Header);
        }

        [Fact]
        public async Task FinMaskedShortLengthViaManagedWebsocketWorks()
        {
            var pipe = DuplexPipe.CreateConnectionPair(PipeOptions.Default, PipeOptions.Default);
            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(pipe.Transport.Input, pipe.Transport.Output), false, null, TimeSpan.FromSeconds(30));

            await webSocket.SendAsync(new ReadOnlyMemory<byte>(new byte[126]), WebSocketMessageType.Binary, true, CancellationToken.None);
            var result = await pipe.Application.Input.ReadAsync();

            var reader = new WebSocketFrameReader();
            SequencePosition pos = default;
            reader.TryParseMessage(result.Buffer, ref pos, ref pos, out var frame);

            var maskingKey = ReadMaskingKey(result.Buffer, 4);
            Assert.Equal(new WebSocketHeader(true, WebSocketOpcode.Binary, true, 126, maskingKey), frame.Header);
        }

        [Fact]
        public async Task FinUnmaskedShortLengthViaManagedWebsocketWorks()
        {
            var pipe = DuplexPipe.CreateConnectionPair(PipeOptions.Default, PipeOptions.Default);
            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(pipe.Transport.Input, pipe.Transport.Output), true, null, TimeSpan.FromSeconds(30));

            await webSocket.SendAsync(new ReadOnlyMemory<byte>(new byte[126]), WebSocketMessageType.Binary, true, CancellationToken.None);
            var result = await pipe.Application.Input.ReadAsync();

            var reader = new WebSocketFrameReader();
            SequencePosition pos = default;
            reader.TryParseMessage(result.Buffer, ref pos, ref pos, out var frame);

            Assert.Equal(new WebSocketHeader(true, WebSocketOpcode.Binary, false, 126, 0), frame.Header);
        }

        [Fact]
        public async Task FinUnmaskedExtendedLengthViaManagedWebsocketWorks()
        {
            var pipe = DuplexPipe.CreateConnectionPair(PipeOptions.Default, PipeOptions.Default);
            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(pipe.Transport.Input, pipe.Transport.Output), true, null, TimeSpan.FromSeconds(30));

            await webSocket.SendAsync(new ReadOnlyMemory<byte>(new byte[ushort.MaxValue + 1]), WebSocketMessageType.Binary, true, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
            var result = await pipe.Application.Input.ReadAsync();

            var reader = new WebSocketFrameReader();
            SequencePosition pos = default;
            reader.TryParseMessage(result.Buffer, ref pos, ref pos, out var frame);

            Assert.Equal(new WebSocketHeader(true, WebSocketOpcode.Binary, false, ushort.MaxValue + 1, 0), frame.Header);
        }

        private int GetMaskingKey() => BitConverter.ToInt32(new byte[] { 1, 2, 3, 4 });

        private int ReadMaskingKey(ReadOnlySequence<byte> seq, int pos) => BinaryPrimitives.ReadInt32BigEndian(seq.FirstSpan.Slice(pos));

        private Memory<byte> GetHeaderBytes(WebSocketHeader header)
        {
            var buffer = new Span<byte>(new byte[14]);

            if (header.Fin)
            {
                buffer[0] = (byte)(128 + header.Opcode);
            }
            else
            {
                buffer[0] = (byte)header.Opcode;
            }

            if (header.Masked)
            {
                buffer[1] = 128;
            }

            var maskPos = 2;
            var payloadLengthSize = 0;

            if (header.PayloadLength <= 125)
            {
                buffer[1] += (byte)header.PayloadLength;
            }
            else if (header.PayloadLength <= ushort.MaxValue)
            {
                buffer[1] += 126;
                var shortSpan = MemoryMarshal.Cast<byte, ushort>(buffer.Slice(2, 2));
                shortSpan[0] = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness((ushort)header.PayloadLength) : (ushort)header.PayloadLength;

                maskPos += 2;
                payloadLengthSize = 2;
            }
            else
            {
                buffer[1] += 127;
                var longSpan = MemoryMarshal.Cast<byte, ulong>(buffer.Slice(2, 8));
                longSpan[0] = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(header.PayloadLength) : header.PayloadLength;

                maskPos += 8;
                payloadLengthSize = 8;
            }

            if(header.Masked)
            {
                var intSpan = MemoryMarshal.Cast<byte, int>(buffer.Slice(maskPos, 4));
                intSpan[0] = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(header.MaskingKey) : header.MaskingKey;
            }

            return new Memory<byte>(buffer.Slice(0, 2 + payloadLengthSize + (header.Masked ? 4 : 0)).ToArray());
        }
    }
}
