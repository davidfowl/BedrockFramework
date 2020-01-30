using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Protocols.WebSockets
{
    /// <summary>
    /// Masks or unmasks a WebSocket payload according to the provided masking key, tracking the
    /// masking key index accross mask or unmasking requests.
    /// </summary>
    internal struct WebSocketPayloadEncoder
    {
        /// <summary>
        /// The masking key to use to mask or unmask the payload.
        /// </summary>
        private int _maskingKey;

        /// <summary>
        /// The current index into the masking key to use to mask or unmask payloads.
        /// </summary>
        private uint _currentMaskIndex;

        /// <summary>
        /// Creates an instance of a WebSocketPayloadEncoder.
        /// </summary>
        /// <param name="maskingKey">The masking key to use to mask or unmask payloads.</param>
        public WebSocketPayloadEncoder(int maskingKey)
        {
            _maskingKey = maskingKey;
            _currentMaskIndex = 0;
        }

        /// <summary>
        /// Masks or unmasks a WebSocket payload as a ReadOnlySequence.
        /// </summary>
        /// <remarks>
        /// Note: Though this takes a ReadOnlySequence, the mask/unmask process will be done to the memory
        /// in-place to avoid allocations.
        /// </remarks>
        /// <param name="input">The sequence to mask or unmask.</param>
        /// <param name="length">The length of the total payload remaining.</param>
        /// <param name="position">The position in the consumed portion of the sequence.</param>
        /// <returns>The number of bytes consumed, which may be less than the number of total payload bytes.</returns>
        public unsafe long MaskUnmaskPayload(in ReadOnlySequence<byte> input, ulong length, out SequencePosition position)
        {
            var lengthRemaining = (long)length;
            var consumed = 0;
            position = input.Start;

            if (input.IsSingleSegment)
            {
                var bytesToRead = (int)Math.Min(lengthRemaining, input.First.Length);
                MaskUnmaskSpan(input.FirstSpan, bytesToRead);

                position = input.GetPosition(bytesToRead);
                return bytesToRead;
            }

            foreach (var memory in input)
            {
                var bytesToRead = (int)Math.Min(lengthRemaining, memory.Length);
                MaskUnmaskSpan(memory.Span, bytesToRead);

                consumed += bytesToRead;
                position = input.GetPosition(consumed);
                lengthRemaining -= bytesToRead;
            }

            return consumed;
        }

        /// <summary>
        /// Masks or unmasks a WebSocket payload span.
        /// </summary>
        /// <param name="span">The span to mask or unmask.</param>
        /// <param name="bytesToRead">The number of bytes to read from the span.</param>
        /// <param name="useSimd">Whether or not to use SIMD accelerated masking.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void MaskUnmaskSpan(in ReadOnlySpan<byte> span, long bytesToRead)
        {
            var maskingKey = _maskingKey;
            var localMaskIndex = _currentMaskIndex;

            fixed (byte* dataStartPtr = &MemoryMarshal.GetReference(span))
            {
                byte* dataPtr = dataStartPtr;
                byte* dataEndPtr = dataStartPtr + bytesToRead;
                byte* maskPtr = (byte*)&maskingKey;

                //If there is at least one int of data available
                if (dataEndPtr - dataStartPtr >= sizeof(int))
                {
                    //Start by aligning to an int boundary, makes aligned loads faster later
                    while ((ulong)dataPtr % sizeof(int) != 0)
                    {
                        Debug.Assert(dataPtr < dataEndPtr);

                        *dataPtr++ ^= maskPtr[localMaskIndex];
                        localMaskIndex = (localMaskIndex + 1) % 4;
                    }

                    //We may have moved forward an uneven number of bytes along the masking key,
                    //generate a new masking key that has those bytes rotated for whole-int unmasking
                    int alignedMask = (int)BitOperations.RotateRight((uint)_maskingKey, (int)localMaskIndex * 8);

                    //Calculate the last possible pointer position that would be able to consume a whole vector
                    var vectorSize = Vector<byte>.Count;
                    var fullVectorReadPtr = dataEndPtr - vectorSize;

                    //If we found we should use SIMD and there is sufficient data to read
                    if (Vector.IsHardwareAccelerated && dataPtr <= fullVectorReadPtr)
                    {
                        Debug.Assert((int)dataPtr % sizeof(int) == 0);

                        //Align by whole ints to full SIMD load boundary to avoid a perf penalty for unaligned loads
                        while ((ulong)dataPtr % (uint)vectorSize != 0)
                        {
                            Debug.Assert(dataPtr < dataEndPtr);

                            *(int*)dataPtr ^= alignedMask;
                            dataPtr += sizeof(int);
                        }

                        //Unmask full aligned vectors at a time
                        if (dataPtr <= fullVectorReadPtr)
                        {
                            Vector<byte> maskVector = Vector.AsVectorByte(new Vector<int>(alignedMask));

                            do
                            {
                                *(Vector<byte>*)dataPtr ^= maskVector;
                                dataPtr += vectorSize;
                            }
                            while (dataPtr <= fullVectorReadPtr);
                        }
                    }

                    //Process remaining data (or all, if couldn't use SIMD) one int at a time.
                    while (dataEndPtr - dataPtr >= sizeof(int))
                    {
                        *(int*)dataPtr ^= alignedMask;
                        dataPtr += sizeof(int);
                    }
                }

                //Remaining data less than size of int
                while (dataPtr != dataEndPtr)
                {
                    *dataPtr++ ^= maskPtr[localMaskIndex];
                    localMaskIndex = (localMaskIndex + 1) % 4;
                }
            }

            _currentMaskIndex = localMaskIndex;
        }
    }
}
