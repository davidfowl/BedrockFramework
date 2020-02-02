using Bedrock.Framework.Protocols.Http.Http1;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Bedrock.Framework.Tests.Protocols
{
    public class Http1HeaderReaderTests
    {
        public static IEnumerable<object[]> IncompleteHeaders()
        {
            var header = "Header: value\r";
            return Enumerable.Range(0, header.Length).Select(x => new[] { header.Substring(0, x) });
        }

        [Theory]
        [MemberData(nameof(IncompleteHeaders))]
        public void ParseMessageReturnsFalseWhenGivenIncompleteHeaders(string rawHeaders)
        {
            var reader = new Http1HeaderReader();
            var buffer = new ReadOnlySequence<byte>(Encoding.ASCII.GetBytes(rawHeaders));
            var consumed = buffer.Start;
            var examined = buffer.Start;

            Assert.False(reader.TryParseMessage(buffer, ref consumed, ref examined, out var message));
            Assert.Equal(default, message);
            Assert.Equal(buffer.Start, consumed);
            Assert.Equal(buffer.Start, examined);
        }

        [Fact]
        public void ParseMessageCanReadHeaderValueWithoutLeadingWhitespace()
        {
            VerifyHeader("Header", "value", "value");
        }

        [Theory]
        [InlineData("Cookie", " ", "")]
        [InlineData("Cookie", "", "")]
        [InlineData("Cookie", "\t", "")]
        [InlineData("Cookie", " \t \t", "")]
        public void ParseMessageCanParseEmptyHeaderValue(
            string rawHeader,
            string expectedHeaderName,
            string expectedHeaderValue)
        {
            VerifyHeader(rawHeader, expectedHeaderName, expectedHeaderValue);
        }

        [Theory]
        [InlineData(" value")]
        [InlineData("  value")]
        [InlineData("\tvalue")]
        [InlineData(" \tvalue")]
        [InlineData("\t value")]
        [InlineData("\t\tvalue")]
        [InlineData("\t\t value")]
        [InlineData(" \t\tvalue")]
        [InlineData(" \t\t value")]
        [InlineData(" \t \t value")]
        public void ParseMessageDoesNotIncludeLeadingWhitespaceInHeaderValue(string rawHeaderValue)
        {
            VerifyHeader("Header", rawHeaderValue, "value");
        }

        [Theory]
        [InlineData("value ")]
        [InlineData("value\t")]
        [InlineData("value \t")]
        [InlineData("value\t ")]
        [InlineData("value\t\t")]
        [InlineData("value\t\t ")]
        [InlineData("value \t\t")]
        [InlineData("value \t\t ")]
        [InlineData("value \t \t ")]
        public void ParseMessageDoesNotIncludeTrailingWhitespaceInHeaderValue(string rawHeaderValue)
        {
            VerifyHeader("Header", rawHeaderValue, "value");
        }

        [Theory]
        [InlineData("one two three")]
        [InlineData("one  two  three")]
        [InlineData("one\ttwo\tthree")]
        [InlineData("one two\tthree")]
        [InlineData("one\ttwo three")]
        [InlineData("one \ttwo \tthree")]
        [InlineData("one\t two\t three")]
        [InlineData("one \ttwo\t three")]
        public void ParseMessagePreservesWhitespaceWithinHeaderValue(string headerValue)
        {
            VerifyHeader("Header", headerValue, headerValue);
        }

        [Fact]
        public void ParseMessageWithGratuitouslySplitBuffers()
        {
            var reader = new Http1HeaderReader();
            var buffer = BytePerSegmentTestSequenceFactory.CreateWithContent(Encoding.ASCII.GetBytes("Connection: keep-alive\r\n"));
            var consumed = buffer.Start;
            var examined = buffer.Start;

            Assert.True(reader.TryParseMessage(buffer, ref consumed, ref examined, out var result));
            Assert.True(result.TryGetValue(out var header));
            Assert.Equal("Connection", Encoding.ASCII.GetString(header.Name));
            Assert.Equal("keep-alive", Encoding.ASCII.GetString(header.Value));
            Assert.Equal(0, buffer.Slice(consumed).Length);
            Assert.Equal(0, buffer.Slice(examined).Length);
        }

        [Theory]
        // Missing CR
        [InlineData("Header: value\n\r\n", 13)]
        [InlineData("Header: value\n", 13)]
        [InlineData("Header: val\nue", 11)]
        // Whitespace in name
        [InlineData(" Header: value\r\n", 0)]
        [InlineData("Header : value\r\n", 6)]
        [InlineData("Hea der: value\r\n", 3)]
        [InlineData("\tHeader: value\r\n", 0)]
        [InlineData("Header\t: value\r\n", 6)]
        [InlineData("Hea\tder: value\r\n", 3)]
        [InlineData("\rHeader: value\r\n", 0)]
        [InlineData("Header\r: value\r\n", 6)]
        [InlineData("Hea\rder: value\r\n", 3)]
        // Empty name
        [InlineData(": value\r\n", 0)]
        // Missing LF
        [InlineData("Header: value\r\r\n", 14)]
        [InlineData("Header: val\rue", 12)]
        // missing colon
        [InlineData("Header value\r\n", 6)]
        [InlineData("Headervalue\r\n", 11)]
        public void ReturnsErrorForInvalidHeader(string invalidHeader, int errorPosition)
        {
            var reader = new Http1HeaderReader();
            var buffer = new ReadOnlySequence<byte>(Encoding.ASCII.GetBytes(invalidHeader));
            var consumed = buffer.Start;
            var examined = buffer.Start;

            Assert.True(reader.TryParseMessage(buffer, ref consumed, ref examined, out var result));
            Assert.False(result.TryGetValue(out _));
            Assert.True(result.TryGetError(out var error));
            Assert.Equal("Invalid Http Header", error.Reason);
            Assert.Equal(invalidHeader.Substring(0, errorPosition + 1), error.Line);
            Assert.Equal(buffer.Start, consumed);
            Assert.Equal(buffer.GetPosition(errorPosition + 1), examined);
        }

        private void VerifyHeader(
            string headerName,
            string rawHeaderValue,
            string expectedHeaderValue)
        {
            var reader = new Http1HeaderReader();
            var buffer = new ReadOnlySequence<byte>(Encoding.ASCII.GetBytes($"{headerName}:{rawHeaderValue}\r\n"));
            var consumed = buffer.Start;
            var examined = buffer.Start;

            Assert.True(reader.TryParseMessage(buffer, ref consumed, ref examined, out var result));
            Assert.True(result.TryGetValue(out var header));
            Assert.Equal(headerName, Encoding.ASCII.GetString(header.Name));
            Assert.Equal(expectedHeaderValue, Encoding.ASCII.GetString(header.Value));
            Assert.Equal(0, buffer.Slice(consumed).Length);
            Assert.Equal(0, buffer.Slice(examined).Length);
        }

        // Doesn't put empty blocks in between every byte
        internal static class BytePerSegmentTestSequenceFactory
        {
            public static ReadOnlySequence<byte> CreateOfSize(int size)
            {
                return CreateWithContent(new byte[size]);
            }

            public static ReadOnlySequence<byte> CreateWithContent(byte[] data)
            {
                var segments = new List<byte[]>();

                foreach (var b in data)
                {
                    segments.Add(new[] { b });
                }

                return CreateSegments(segments.ToArray());
            }

            public static ReadOnlySequence<byte> CreateSegments(params byte[][] inputs)
            {
                if (inputs == null || inputs.Length == 0)
                {
                    throw new InvalidOperationException();
                }

                var i = 0;

                BufferSegment last = null;
                BufferSegment first = null;

                do
                {
                    var s = inputs[i];
                    var length = s.Length;
                    var dataOffset = length;
                    var chars = new byte[length * 2];

                    for (var j = 0; j < length; j++)
                    {
                        chars[dataOffset + j] = s[j];
                    }

                    // Create a segment that has offset relative to the OwnedMemory and OwnedMemory itself has offset relative to array
                    var memory = new Memory<byte>(chars).Slice(length, length);

                    if (first == null)
                    {
                        first = new BufferSegment(memory);
                        last = first;
                    }
                    else
                    {
                        last = last.Append(memory);
                    }
                    i++;
                } while (i < inputs.Length);

                return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
            }
        }

        private class BufferSegment : ReadOnlySequenceSegment<byte>
        {
            public BufferSegment(Memory<byte> memory)
            {
                Memory = memory;
            }

            public BufferSegment Append(Memory<byte> memory)
            {
                var segment = new BufferSegment(memory)
                {
                    RunningIndex = RunningIndex + Memory.Length
                };
                Next = segment;
                return segment;
            }
        }
    }
}
