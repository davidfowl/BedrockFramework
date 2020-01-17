using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Tests.Infrastructure
{
    /// <summary>
    /// A utility class for creating sequence segments from byte arrays during unit testing.
    /// </summary>
    public class TestSequenceSegment : ReadOnlySequenceSegment<byte>
    {
        /// <summary>
        /// Creates an instance of a TestSequenceSegment.
        /// </summary>
        /// <param name="data">The data to be contained in this segment.</param>
        public TestSequenceSegment(byte[] data)
        {
            Memory = new Memory<byte>(data);
        }

        /// <summary>
        /// Adds a segment to the list of sequence segments.
        /// </summary>
        /// <param name="data">The data to be contained in the added segment.</param>
        /// <returns>The new sequence segment.</returns>
        public TestSequenceSegment AddSegment(byte[] data)
        {
            var segment = new TestSequenceSegment(data);
            segment.RunningIndex = RunningIndex + Memory.Length;

            Next = segment;
            return segment;
        }
    }
}
