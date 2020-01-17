﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
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
        private int _currentMaskIndex;

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
        /// <param name="useSimd">Whether or not to use SIMD accelerated masking.</param>
        /// <param name="position">The position in the consumed portion of the sequence.</param>
        /// <returns>The number of bytes consumed, which may be less than the number of total payload bytes.</returns>
        public unsafe long MaskUnmaskPayload(in ReadOnlySequence<byte> input, ulong length, bool useSimd, out SequencePosition position)
        {
            var lengthRemaining = (long)length;
            position = input.Start;

            if(input.IsSingleSegment)
            {
                var bytesToRead = (int)Math.Min((long)lengthRemaining, input.First.Length);
                MaskUnmaskSpan(input.FirstSpan, bytesToRead, useSimd);

                position = new SequencePosition(input.First, bytesToRead);
                return lengthRemaining - bytesToRead;
            }

            foreach (var memory in input)
            {
                var bytesToRead = (int)Math.Min((long)lengthRemaining, memory.Length);
                MaskUnmaskSpan(memory.Span, bytesToRead, useSimd);

                position = new SequencePosition(memory, bytesToRead);
                lengthRemaining -= bytesToRead;
            }

            return lengthRemaining;
        }

        /// <summary>
        /// Masks or unmasks a WebSocket payload span.
        /// </summary>
        /// <param name="span">The span to mask or unmask.</param>
        /// <param name="bytesToRead">The number of bytes to read from the span.</param>
        /// <param name="useSimd">Whether or not to use SIMD accelerated masking.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void MaskUnmaskSpan(in ReadOnlySpan<byte> span, long bytesToRead, bool useSimd)
        {
            var maskingKey = _maskingKey;

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

                        *dataPtr++ ^= maskPtr[_currentMaskIndex];
                        _currentMaskIndex = (_currentMaskIndex + 1) & 3; //Modulus shortcut for power of 2 values for perf, same as _currentMaskIndex % 4
                    }

                    //We may have moved forward an uneven number of bytes along the masking key,
                    //generate a new masking key that has those bytes rotated for whole-int unmasking
                    int alignedMask = (int)BitOperations.RotateRight((uint)_maskingKey, _currentMaskIndex * 8);

                    //If we found we should use SIMD and there is sufficient data to read
                    if (useSimd && (dataEndPtr - dataPtr) >= Vector<byte>.Count)
                    {
                        //Align by whole ints to full SIMD load boundary to avoid a perf penalty for unaligned loads
                        while ((ulong)dataPtr % (uint)Vector<byte>.Count != 0)
                        {
                            Debug.Assert(dataPtr < dataEndPtr);

                            *(int*)dataPtr ^= alignedMask;
                            dataPtr += sizeof(int);
                        }

                        //Unmask full aligned vectors at a time
                        if (dataEndPtr - dataPtr >= Vector<byte>.Count)
                        {
                            Vector<byte> maskVector = Vector.AsVectorByte(new Vector<int>(alignedMask));

                            do
                            {
                                *(Vector<byte>*)dataPtr ^= maskVector;
                                dataPtr += Vector<byte>.Count;
                            }
                            while (dataEndPtr - dataPtr >= Vector<byte>.Count);
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
                    *dataPtr++ ^= maskPtr[_currentMaskIndex];
                    _currentMaskIndex = (_currentMaskIndex + 1) & 3;
                }
            }
        }
    }
}
