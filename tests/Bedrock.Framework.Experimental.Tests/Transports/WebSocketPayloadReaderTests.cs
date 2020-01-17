using Bedrock.Framework.Experimental.Tests.Infrastructure;
using Bedrock.Framework.Experimental.Transports.WebSockets;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Bedrock.Framework.Experimental.Tests.Transports
{
    public class WebSocketPayloadReaderTests
    {
        private RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();

        [Fact]
        public void SingleSegmentSequenceWorks()
        {
            var maskingKey = GenerateMaskingKey();
            var payloadString = "This is a test payload.";
            var payload = GenerateMaskedPayload(payloadString, maskingKey);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader
            {
                Fin = true,
                Masked = true,
                MaskingKey = maskingKey,
                Opcode = WebSocketOpcode.Binary,
                PayloadLength = (ulong)payload.Length
            });

            var sequence = new ReadOnlySequence<byte>(payload);

            SequencePosition pos = default;
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.First.ToArray()));
        }

        [Fact]
        public void MultiSegmentSequenceWorks()
        {
            var maskingKey = GenerateMaskingKey();
            var payloadString = "This is a test payload.";
            var sequence = SegmentArray(GenerateMaskedPayload(payloadString, maskingKey), 4);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader
            {
                Fin = true,
                Masked = true,
                MaskingKey = maskingKey,
                Opcode = WebSocketOpcode.Binary,
                PayloadLength = (ulong)sequence.Length
            });

            SequencePosition pos = default;
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.ToArray()));
        }

        [Fact]
        public void MultiSegmentWithZeroLengthSegmentsSequenceWorks()
        {
            var maskingKey = GenerateMaskingKey();
            var payloadString = "This is a test payload.";
            var masked = GenerateMaskedPayload(payloadString, maskingKey);

            var left = new TestSequenceSegment(masked.AsSpan(0..10).ToArray());
            var middle = left.AddSegment(new byte[0]);
            var right = middle.AddSegment(masked.AsSpan(10..masked.Length).ToArray());

            var sequence = new ReadOnlySequence<byte>(left, 0, right, right.Memory.Length);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader
            {
                Fin = true,
                Masked = true,
                MaskingKey = maskingKey,
                Opcode = WebSocketOpcode.Binary,
                PayloadLength = (ulong)sequence.Length
            });

            SequencePosition pos = default;
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.ToArray()));
        }

        [Fact]
        public void LargeSequenceWorks()
        {
            var maskingKey = GenerateMaskingKey();
            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 250));
            var sequence = SegmentArray(GenerateMaskedPayload(payloadString, maskingKey), 4);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader
            {
                Fin = true,
                Masked = true,
                MaskingKey = maskingKey,
                Opcode = WebSocketOpcode.Binary,
                PayloadLength = (ulong)sequence.Length
            });

            SequencePosition pos = default;
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.ToArray()));
        }

        [Fact]
        public void HugeSequenceWorks()
        {
            var maskingKey = GenerateMaskingKey();
            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 25000));
            var sequence = SegmentArray(GenerateMaskedPayload(payloadString, maskingKey), 4);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader
            {
                Fin = true,
                Masked = true,
                MaskingKey = maskingKey,
                Opcode = WebSocketOpcode.Binary,
                PayloadLength = (ulong)sequence.Length
            });

            SequencePosition pos = default;
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(payloadString, Encoding.UTF8.GetString(outputSequence.ToArray()));
        }

        [Fact]
        public void EmptySequenceReturnsFalse()
        {
            var sequence = new ReadOnlySequence<byte>(new byte[0]);
            var encoder = new WebSocketPayloadReader(new WebSocketHeader
            {
                Fin = true,
                Masked = false,
                Opcode = WebSocketOpcode.Binary,
                PayloadLength = 1
            });

            SequencePosition pos = default;
            var success = encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.False(success);
        }

        [Fact]
        public void SequenceLongerThanPayloadLengthWorks()
        {
            var maskingKey = GenerateMaskingKey();
            var payloadString = "This is a test payload.";
            var payload = GenerateMaskedPayload(payloadString, maskingKey);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader
            {
                Fin = true,
                Masked = true,
                MaskingKey = maskingKey,
                Opcode = WebSocketOpcode.Binary,
                PayloadLength = (ulong)payload.Length
            });

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
            var maskingKey = GenerateMaskingKey();
            var payloadString = "This is a test payload.";
            var payload = GenerateMaskedPayload(payloadString, maskingKey);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader
            {
                Fin = true,
                Masked = true,
                MaskingKey = maskingKey,
                Opcode = WebSocketOpcode.Binary,
                PayloadLength = (ulong)payload.Length
            });

            var sequence = new ReadOnlySequence<byte>(payload, 0, payload.Length - 9);

            SequencePosition pos = default;
            encoder.TryParseMessage(in sequence, ref pos, ref pos, out var outputSequence);

            Assert.Equal(pos, sequence.GetPosition(payload.Length - 9));
            Assert.Equal(payloadString.Substring(0, payload.Length - 9), Encoding.UTF8.GetString(outputSequence.First.ToArray()));
        }

        [Fact]
        public void MaskingWorksAcrossMultipleParseCalls()
        {
            var maskingKey = GenerateMaskingKey();
            var payloadString = String.Join(String.Empty, Enumerable.Repeat("This is a test payload.", 250));
            var sequence = SegmentArray(GenerateMaskedPayload(payloadString, maskingKey), 4);

            var encoder = new WebSocketPayloadReader(new WebSocketHeader
            {
                Fin = true,
                Masked = true,
                MaskingKey = maskingKey,
                Opcode = WebSocketOpcode.Binary,
                PayloadLength = (ulong)sequence.Length
            });

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

        private int GenerateMaskingKey()
        {
            var maskBytes = new byte[4];
            _rng.GetBytes(maskBytes);

            return BitConverter.ToInt32(maskBytes, 0);
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
