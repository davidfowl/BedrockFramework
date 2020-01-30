using Bedrock.Framework.Tests.Infrastructure;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace System
{
    public static class ArrayExtensions
    {
        public static IEnumerable<T[]> Split<T>(this T[] data, int numSegments)
        {
            var stride = data.Length / numSegments;

            var totalConsumed = 0;
            for (var i = 0; i < data.Length - stride; i += stride)
            {
                var slice = new T[stride];
                Array.Copy(data, i, slice, 0, stride);

                totalConsumed += stride;
                yield return slice;
            }

            if (totalConsumed < data.Length)
            {
                var sliceLength = data.Length - totalConsumed;
                var finalSlice = new T[sliceLength];

                Array.Copy(data, totalConsumed, finalSlice, 0, sliceLength);
                yield return finalSlice;
            }
        }

        public static ReadOnlySequence<byte> ToReadOnlySequence(this byte[] data, int numSegments)
        {
            TestSequenceSegment currentSegment = null;
            TestSequenceSegment firstSegment = null;

            foreach (var slice in Split(data, numSegments))
            {
                if (currentSegment == null)
                {
                    currentSegment = new TestSequenceSegment(slice);
                    firstSegment = currentSegment;
                }
                else
                {
                    currentSegment = currentSegment.AddSegment(slice);
                }
            }

            return new ReadOnlySequence<byte>(firstSegment, 0, currentSegment, currentSegment.Memory.Length);
        }
    }
}
