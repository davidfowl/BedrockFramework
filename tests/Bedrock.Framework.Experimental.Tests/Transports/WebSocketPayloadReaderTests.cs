using Bedrock.Framework.Experimental.Tests.Infrastructure;
using Bedrock.Framework.Experimental.Transports.WebSockets;
using System;
using System.Buffers;
using System.Buffers.Binary;
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
    public class WebSocketPayloadReaderTests
    {
        private RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();

        [Fact]
        public void SingleSegmentSequenceWorks()
        {
            var maskingKey = WebSocketHeader.GenerateMaskingKey();
            var payloadString = "This is a test payload.";
            var payload = GenerateMaskedPayload(payloadString, maskingKey);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)payload.Length, maskingKey));
            var sequence = new ReadOnlySequence<byte>(payload);

            SequencePosition pos = default;
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.First.ToArray()));
        }

        [Fact]
        public void MultiSegmentSequenceWorks()
        {
            var maskingKey = WebSocketHeader.GenerateMaskingKey();
            var payloadString = "This is a test payload.";
            var sequence = SegmentArray(GenerateMaskedPayload(payloadString, maskingKey), 4);

            SequencePosition pos = default;
            var encoder = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)sequence.Length, maskingKey));
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.ToArray()));
        }

        [Fact]
        public void MultiSegmentWithZeroLengthSegmentsSequenceWorks()
        {
            var maskingKey = WebSocketHeader.GenerateMaskingKey();
            var payloadString = "This is a test payload.";
            var masked = GenerateMaskedPayload(payloadString, maskingKey);

            var left = new TestSequenceSegment(masked.AsSpan(0..10).ToArray());
            var middle = left.AddSegment(new byte[0]);
            var right = middle.AddSegment(masked.AsSpan(10..masked.Length).ToArray());

            var sequence = new ReadOnlySequence<byte>(left, 0, right, right.Memory.Length);

            SequencePosition pos = default;
            var encoder = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)sequence.Length, maskingKey));
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.ToArray()));
        }

        [Fact]
        public void LargeSequenceWorks()
        {
            var maskingKey = WebSocketHeader.GenerateMaskingKey();
            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 250));
            var sequence = SegmentArray(GenerateMaskedPayload(payloadString, maskingKey), 4);

            SequencePosition pos = default;
            var encoder = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)sequence.Length, maskingKey));
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.ToArray()));
        }

        [Fact]
        public void HugeSequenceWorks()
        {
            var maskingKey = WebSocketHeader.GenerateMaskingKey();
            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 25000));
            var sequence = SegmentArray(GenerateMaskedPayload(payloadString, maskingKey), 4);

            SequencePosition pos = default;
            var encoder = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)sequence.Length, maskingKey));
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.ToArray()));
        }

        [Fact]
        public void EmptySequenceReturnsFalse()
        {
            var sequence = new ReadOnlySequence<byte>(new byte[0]);
            var encoder = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, 1, default));

            SequencePosition pos = default;
            var success = encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.False(success);
        }

        [Fact]
        public void SequenceLongerThanPayloadLengthWorks()
        {
            var maskingKey = WebSocketHeader.GenerateMaskingKey();
            var payloadString = "This is a test payload.";
            var payload = GenerateMaskedPayload(payloadString, maskingKey);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)payload.Length, maskingKey));

            var first = new TestSequenceSegment(payload);
            var last = first.AddSegment(new byte[64]);
            var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

            SequencePosition pos = default;
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(pos, sequence.GetPosition(payload.Length));
            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.First.ToArray()));
        }

        [Fact]
        public void SequenceShorterThanPayloadLengthWorks()
        {
            var maskingKey = WebSocketHeader.GenerateMaskingKey();
            var payloadString = "This is a test payload.";
            var payload = GenerateMaskedPayload(payloadString, maskingKey);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)payload.Length, maskingKey));
            var sequence = new ReadOnlySequence<byte>(payload, 0, payload.Length - 9);

            SequencePosition pos = default;
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(pos, sequence.GetPosition(payload.Length - 9));
            Assert.Equal(payloadString.Substring(0, payload.Length - 9), Encoding.UTF8.GetString(outputSequence.First.ToArray()));
        }

        [Fact]
        public void MaskingWorksAcrossMultipleParseCalls()
        {
            var maskingKey = WebSocketHeader.GenerateMaskingKey();
            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 250));
            var sequence = SegmentArray(GenerateMaskedPayload(payloadString, maskingKey), 4);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)sequence.Length, maskingKey));
            var resultData = new byte[0];
            SequencePosition pos = default;

            foreach(var memory in sequence)
            {
                var localSequence = new ReadOnlySequence<byte>(memory);
                encoder.TryParseMessage(in localSequence, ref pos, ref pos, out var outputSequence);

                var toAppend = new byte[resultData.Length + outputSequence.Length];

                Array.Copy(resultData, 0, toAppend, 0, resultData.Length);
                Array.Copy(outputSequence.ToArray(), 0, toAppend, resultData.Length, outputSequence.Length);

                resultData = toAppend;
            }

            Assert.Equal(payloadString, Encoding.UTF8.GetString(resultData));
        }

        [Fact]
        public async Task ShortMaskedViaManagedWebsocketWorks()
        {
            var options = new PipeOptions(useSynchronizationContext: false);
            var duplexPipe = DuplexPipe.CreateConnectionPair(options, options);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(duplexPipe.Transport.Input, duplexPipe.Transport.Output), false, null, TimeSpan.FromSeconds(30));
            var data = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("This is a test payload."));

            await webSocket.SendAsync(data, WebSocketMessageType.Binary, true, default);
            var result = await duplexPipe.Application.Input.ReadAsync();

            var maskingKey = ReadMaskingKey(result.Buffer, 2);
            duplexPipe.Application.Input.AdvanceTo(result.Buffer.GetPosition(6));
            result = await duplexPipe.Application.Input.ReadAsync();

            var reader = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, 23, maskingKey));
            SequencePosition pos = default;
            reader.TryParseMessage(result.Buffer, ref pos, ref pos, out var message);

            var resultString = Encoding.UTF8.GetString(result.Buffer.ToArray());
            Assert.Equal("This is a test payload.", resultString);
        }

        [Fact]
        public async Task MediumMaskedViaManagedWebsocketWorks()
        {
            var options = new PipeOptions(useSynchronizationContext: false);
            var duplexPipe = DuplexPipe.CreateConnectionPair(options, options);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(duplexPipe.Transport.Input, duplexPipe.Transport.Output), false, null, TimeSpan.FromSeconds(30));
            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 25));
            var data = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payloadString));

            await webSocket.SendAsync(data, WebSocketMessageType.Binary, true, default);
            var result = await duplexPipe.Application.Input.ReadAsync();

            var maskingKey = ReadMaskingKey(result.Buffer, 4);
            duplexPipe.Application.Input.AdvanceTo(result.Buffer.GetPosition(8));
            result = await duplexPipe.Application.Input.ReadAsync();

            var reader = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)data.Length, maskingKey));
            SequencePosition pos = default;
            reader.TryParseMessage(result.Buffer, ref pos, ref pos, out var message);

            var resultString = Encoding.UTF8.GetString(result.Buffer.ToArray());
            Assert.Equal(payloadString, resultString);
        }

        [Fact]
        public async Task LongMaskedViaManagedWebsocketWorks()
        {
            var options = new PipeOptions(useSynchronizationContext: false);
            var duplexPipe = DuplexPipe.CreateConnectionPair(options, options);

            var webSocket = WebSocket.CreateFromStream(new DuplexPipeStream(duplexPipe.Transport.Input, duplexPipe.Transport.Output), false, null, TimeSpan.FromSeconds(30));
            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 2500));
            var data = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(payloadString));

            var writeTask = webSocket.SendAsync(data, WebSocketMessageType.Binary, true, default);
            var result = await duplexPipe.Application.Input.ReadAsync();

            var maskingKey = ReadMaskingKey(result.Buffer, 4);
            duplexPipe.Application.Input.AdvanceTo(result.Buffer.GetPosition(8));
            result = await duplexPipe.Application.Input.ReadAsync();

            var reader = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)data.Length, maskingKey));
            SequencePosition pos = default;
            reader.TryParseMessage(result.Buffer, ref pos, ref pos, out var message);

            var resultString = Encoding.UTF8.GetString(result.Buffer.ToArray());
            duplexPipe.Application.Input.AdvanceTo(result.Buffer.End);
            await writeTask;

            Assert.Equal(payloadString, resultString);
        }

        private int ReadMaskingKey(ReadOnlySequence<byte> seq, int pos) => BitConverter.IsLittleEndian
            ? BinaryPrimitives.ReadInt32LittleEndian(seq.FirstSpan.Slice(pos))
            : BinaryPrimitives.ReadInt32BigEndian(seq.FirstSpan.Slice(pos));

        private byte[] GenerateMaskedPayload(string payload, int mask)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            var maskBytes = BitConverter.GetBytes(mask);

            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] ^= maskBytes[i % 4];
            }

            return bytes;
        }

        private ReadOnlySequence<byte> SegmentArray(byte[] data, int numSegments)
        {
            TestSequenceSegment currentSegment = null;
            TestSequenceSegment firstSegment = null;

            var stride = data.Length / numSegments;

            var totalConsumed = 0;
            for (var i = 0; i < data.Length - stride; i += stride)
            {
                var slice = new byte[stride];
                Array.Copy(data, i, slice, 0, stride);

                if (currentSegment == null)
                {
                    currentSegment = new TestSequenceSegment(slice);
                    firstSegment = currentSegment;
                }
                else
                {
                    currentSegment = currentSegment.AddSegment(slice);
                }

                totalConsumed += stride;
            }

            if (totalConsumed < data.Length)
            {
                var sliceLength = data.Length - totalConsumed;
                var finalSlice = new byte[sliceLength];

                Array.Copy(data, totalConsumed, finalSlice, 0, sliceLength);
                currentSegment = currentSegment.AddSegment(finalSlice);
            }

            return new ReadOnlySequence<byte>(firstSegment, 0, currentSegment, currentSegment.Memory.Length);
        }
    }
}
