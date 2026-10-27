using System;
using System.Buffers;
using System.Linq;
using System.Text;
using Bedrock.Framework.Experimental.Tests.Infrastructure;
using Xunit;

namespace Bedrock.Framework.Protocols.WebSockets.Tests
{
    /// <summary>
    /// Regression and RFC 6455 section 5 conformance tests for the frame codec.
    /// </summary>
    public class WebSocketCodecConformanceTests
    {
        [Theory]
        [InlineData(true, 1)]
        [InlineData(true, 4)]
        [InlineData(false, 1)]
        [InlineData(false, 4)]
        public void PayloadReaderTracksRemainingBytesToZero(bool masked, int segments)
        {
            var payload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("payload ", 40)));
            var maskingKey = masked ? WebSocketHeader.GenerateMaskingKey() : 0;
            var wire = masked ? Mask(payload, maskingKey) : payload.ToArray();

            var reader = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, masked, (ulong)payload.Length, maskingKey));
            var sequence = Segment(wire, segments);

            SequencePosition consumed = default, examined = default;
            Assert.True(reader.TryParseMessage(sequence, ref consumed, ref examined, out var output));

            Assert.True(reader.IsComplete);
            Assert.Equal(0UL, reader.BytesRemaining);
            Assert.Equal(payload, output.ToArray());
        }

        [Fact]
        public void PayloadReaderTracksRemainingAcrossPartialReads()
        {
            var payload = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("abcdefg", 30)));
            var maskingKey = WebSocketHeader.GenerateMaskingKey();
            var wire = Mask(payload, maskingKey);
            var reader = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, true, (ulong)payload.Length, maskingKey));

            var result = new byte[0];
            var offset = 0;
            foreach (var chunk in new[] { 1, 3, 7, 50, 200 })
            {
                var take = Math.Min(chunk, wire.Length - offset);
                if (take == 0)
                {
                    break;
                }

                var seq = new ReadOnlySequence<byte>(wire, offset, take);
                SequencePosition consumed = default, examined = default;
                Assert.True(reader.TryParseMessage(seq, ref consumed, ref examined, out var output));

                Assert.Equal(seq.GetPosition(take), consumed);
                result = result.Concat(output.ToArray()).ToArray();
                offset += take;

                Assert.Equal((ulong)(payload.Length - offset), reader.BytesRemaining);
            }

            Assert.True(reader.IsComplete);
            Assert.Equal(payload, result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void PayloadReaderDoesNotReturnBytesBeyondPayload(bool masked)
        {
            var payload = Encoding.UTF8.GetBytes("This is a test payload.");
            var maskingKey = masked ? WebSocketHeader.GenerateMaskingKey() : 0;
            var wire = masked ? Mask(payload, maskingKey) : payload.ToArray();

            var first = new TestSequenceSegment(wire);
            var last = first.AddSegment(Enumerable.Repeat((byte)0xFF, 64).ToArray());
            var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);

            var reader = new WebSocketPayloadReader(new WebSocketHeader(true, WebSocketOpcode.Binary, masked, (ulong)payload.Length, maskingKey));
            SequencePosition consumed = default, examined = default;
            reader.TryParseMessage(sequence, ref consumed, ref examined, out var output);

            Assert.Equal(payload, output.ToArray());
            Assert.Equal(sequence.GetPosition(payload.Length), consumed);

            // The trailing bytes belonging to the next frame must be untouched
            Assert.All(last.Memory.ToArray(), b => Assert.Equal(0xFF, b));
        }

        [Fact]
        public void EncoderReturnsBytesProcessed()
        {
            var encoder = new WebSocketPayloadEncoder(WebSocketHeader.GenerateMaskingKey());
            var data = new byte[100];

            Assert.Equal(40, encoder.MaskUnmaskPayload(new ReadOnlySequence<byte>(data), 40, out _));
            Assert.Equal(100, encoder.MaskUnmaskPayload(Segment(data, 3), 1000, out var position));
            Assert.Equal(100, Segment(data, 3).Slice(0, position).Length);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(500)]
        [InlineData(70000)]
        public void WriterDoesNotMutateCallerPayload(int length)
        {
            var payload = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
            var copy = payload.ToArray();

            var header = WebSocketHeader.CreateMasked(true, WebSocketOpcode.Binary, (ulong)length);
            var frame = new WebSocketWriteFrame(header, Segment(payload, 3));
            var output = new ArrayBufferWriter<byte>();

            new WebSocketFrameWriter().WriteMessage(frame, output);
            Assert.Equal(copy, payload);

            // Writing the same frame twice must produce identical bytes
            var second = new ArrayBufferWriter<byte>();
            new WebSocketFrameWriter().WriteMessage(frame, second);
            Assert.Equal(output.WrittenSpan.ToArray(), second.WrittenSpan.ToArray());

            // And the frame must decode back to the original payload
            var (decodedHeader, decodedPayload) = Decode(output.WrittenMemory);
            Assert.Equal(header, decodedHeader);
            Assert.Equal(copy, decodedPayload);
        }

        [Theory]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        [InlineData(true, true, true)]
        public void ReservedBitsRoundTrip(bool rsv1, bool rsv2, bool rsv3)
        {
            var header = new WebSocketHeader(true, WebSocketOpcode.Text, false, 3, 0, rsv1, rsv2, rsv3);
            var output = new ArrayBufferWriter<byte>();
            new WebSocketFrameWriter().WriteMessage(new WebSocketWriteFrame(header, new ReadOnlySequence<byte>(new byte[3])), output);

            var (decoded, _) = Decode(output.WrittenMemory);
            Assert.Equal(rsv1, decoded.Rsv1);
            Assert.Equal(rsv2, decoded.Rsv2);
            Assert.Equal(rsv3, decoded.Rsv3);
        }

        [Theory]
        [InlineData(0x3)]
        [InlineData(0x4)]
        [InlineData(0x5)]
        [InlineData(0x6)]
        [InlineData(0x7)]
        [InlineData(0xB)]
        [InlineData(0xC)]
        [InlineData(0xD)]
        [InlineData(0xE)]
        [InlineData(0xF)]
        public void ReservedOpcodesThrow(byte opcode)
        {
            Assert.Throws<WebSocketFrameException>(() => Parse(new byte[] { (byte)(0x80 | opcode), 0x00 }));
        }

        [Theory]
        [InlineData(WebSocketOpcode.Close)]
        [InlineData(WebSocketOpcode.Ping)]
        [InlineData(WebSocketOpcode.Pong)]
        public void FragmentedControlFramesThrow(WebSocketOpcode opcode)
        {
            Assert.Throws<WebSocketFrameException>(() => Parse(new byte[] { (byte)opcode, 0x00 }));
        }

        [Theory]
        [InlineData(WebSocketOpcode.Close)]
        [InlineData(WebSocketOpcode.Ping)]
        [InlineData(WebSocketOpcode.Pong)]
        public void OversizedControlFramesThrow(WebSocketOpcode opcode)
        {
            Assert.Throws<WebSocketFrameException>(() => Parse(new byte[] { (byte)(0x80 | (byte)opcode), 126, 0x00, 0x7E }));
        }

        [Fact]
        public void ControlFrameWith125BytesIsAccepted()
        {
            var frame = Parse(new byte[] { 0x89, 125 });
            Assert.Equal(125UL, frame.Header.PayloadLength);
            Assert.True(frame.Header.IsControlFrame);
        }

        [Fact]
        public void SixtyFourBitLengthWithMostSignificantBitSetThrows()
        {
            Assert.Throws<WebSocketFrameException>(() => Parse(new byte[] { 0x82, 127, 0x80, 0, 0, 0, 0, 0, 0, 0 }));
        }

        [Fact]
        public void ConsumedPositionPointsAtStartOfPayload()
        {
            var bytes = new byte[] { 0x82, 0xFE, 0x01, 0x00, 1, 2, 3, 4, 0xAA };
            var sequence = new ReadOnlySequence<byte>(bytes);
            SequencePosition consumed = default, examined = default;

            Assert.True(new WebSocketFrameReader().TryParseMessage(sequence, ref consumed, ref examined, out var frame));
            Assert.Equal(sequence.GetPosition(8), consumed);
            Assert.Equal(256UL, frame.Header.PayloadLength);
            Assert.True(frame.Header.Masked);
        }

        private static WebSocketReadFrame Parse(byte[] bytes)
        {
            var sequence = new ReadOnlySequence<byte>(bytes);
            SequencePosition consumed = default, examined = default;
            new WebSocketFrameReader().TryParseMessage(sequence, ref consumed, ref examined, out var frame);
            return frame;
        }

        private static (WebSocketHeader, byte[]) Decode(ReadOnlyMemory<byte> bytes)
        {
            var sequence = new ReadOnlySequence<byte>(bytes);
            SequencePosition consumed = default, examined = default;
            Assert.True(new WebSocketFrameReader().TryParseMessage(sequence, ref consumed, ref examined, out var frame));

            // Copy so in-place unmasking does not touch the caller's buffer
            var payloadSequence = new ReadOnlySequence<byte>(sequence.Slice(consumed).ToArray());
            var payloadReader = frame.Payload;
            payloadReader.TryParseMessage(payloadSequence, ref consumed, ref examined, out var payload);
            return (frame.Header, payload.ToArray());
        }

        private static byte[] Mask(byte[] payload, int maskingKey)
        {
            var maskBytes = BitConverter.GetBytes(maskingKey);
            var result = payload.ToArray();
            for (var i = 0; i < result.Length; i++)
            {
                result[i] ^= maskBytes[i % 4];
            }
            return result;
        }

        private static ReadOnlySequence<byte> Segment(byte[] data, int segments)
        {
            if (segments <= 1)
            {
                return new ReadOnlySequence<byte>(data);
            }

            var stride = (data.Length + segments - 1) / segments;
            TestSequenceSegment first = null, current = null;
            for (var i = 0; i < data.Length; i += stride)
            {
                var slice = data.AsSpan(i, Math.Min(stride, data.Length - i)).ToArray();
                current = current == null ? (first = new TestSequenceSegment(slice)) : current.AddSegment(slice);
            }

            return new ReadOnlySequence<byte>(first, 0, current, current.Memory.Length);
        }
    }
}
