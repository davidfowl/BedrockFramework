using Bedrock.Framework.Experimental.Transports.WebSockets;
using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
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

        public int GetMaskingKey() => BitConverter.ToInt32(new byte[] { 1, 2, 3, 4 });

        public Memory<byte> GetHeaderBytes(WebSocketHeader header)
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
                intSpan[0] = header.MaskingKey;
            }

            return new Memory<byte>(buffer.Slice(0, 2 + payloadLengthSize + (header.Masked ? 4 : 0)).ToArray());
        }
    }
}
