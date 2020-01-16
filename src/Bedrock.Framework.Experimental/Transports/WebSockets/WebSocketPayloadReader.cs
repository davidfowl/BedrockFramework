using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Experimental.Transports.WebSockets
{
    public struct WebSocketPayloadReader : IMessageReader<ReadOnlySequence<byte>>
    {
        private ulong _payloadBytesRemaining;

        private int _maskingKey;

        private bool _useSimd;

        private int _currentMaskIndex;

        public WebSocketPayloadReader(WebSocketHeader header)
        {
            _payloadBytesRemaining = header.PayloadLength;
            _maskingKey = header.MaskingKey;
            _useSimd = Vector.IsHardwareAccelerated && Vector<byte>.Count % sizeof(int) == 0;
            _currentMaskIndex = 0;
        }

        public unsafe bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out ReadOnlySequence<byte> message)
        {
            if(_payloadBytesRemaining == 0)
            {
                message = default;
                consumed = input.Start;
                examined = input.Start;
                return true;
            }

            if(input.IsEmpty)
            {
                message = input;
                consumed = input.Start;
                examined = input.Start;
                return false;
            }

            foreach(var memory in input)
            {
                var bytesToRead = (int)Math.Min((long)_payloadBytesRemaining, memory.Length);
                var maskingKey = _maskingKey;

                fixed (byte* dataStartPtr = &MemoryMarshal.GetReference(memory.Span))
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
                        if (_useSimd && (dataEndPtr - dataPtr) >= Vector<byte>.Count)
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

                var position = new SequencePosition(memory, bytesToRead);
                _payloadBytesRemaining -= (ulong)bytesToRead;
                consumed = position;
                examined = position;
            }

            message = input;
            return true;
        }
    }
}
