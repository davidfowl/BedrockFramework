﻿using System;
using System.Buffers;
using System.Diagnostics;

namespace Bedrock.Framework.Protocols
{
    internal class ConsumableArrayBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[] _buffer;
        private int _index;
        private int _consumedCount;

        private const int DefaultInitialBufferSize = 256;

        /// <summary>
        /// Creates an instance of an <see cref="ConsumableArrayBufferWriter{byte}"/>, in which data can be written to,
        /// with the default initial capacity.
        /// </summary>
        public ConsumableArrayBufferWriter()
        {
            _buffer = Array.Empty<byte>();
            _index = 0;
            _consumedCount = 0;
        }

        /// <summary>
        /// Creates an instance of an <see cref="ConsumableArrayBufferWriter{byte}"/>, in which data can be written to,
        /// with an initial capacity specified.
        /// </summary>
        /// <param name="initialCapacity">The minimum capacity with which to initialize the underlying buffer.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="initialCapacity"/> is not positive (i.e. less than or equal to 0).
        /// </exception>
        public ConsumableArrayBufferWriter(int initialCapacity)
        {
            if (initialCapacity <= 0)
                throw new ArgumentException(nameof(initialCapacity));

            _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
            _index = 0;
            _consumedCount = 0;
        }

        /// <summary>
        /// Returns the unconsumed data written to the underlying buffer so far, as a <see cref="ReadOnlyMemory{byte}"/>.
        /// </summary>
        public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(_consumedCount.._index);

        /// <summary>
        /// Returns the unconsumed data written to the underlying buffer so far, as a <see cref="ReadOnlySpan{byte}"/>.
        /// </summary>
        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(_consumedCount.._index);

        /// <summary>
        /// Returns the amount of unconsumed data written to the underlying buffer so far.
        /// </summary>
        public int UnconsumedWrittenCount => _index - _consumedCount;

        /// <summary>
        /// Returns the total amount of space within the underlying buffer.
        /// </summary>
        public int Capacity => _buffer.Length;

        /// <summary>
        /// Returns the amount of space available that can still be written into without forcing the underlying buffer to grow or shift the data.
        /// </summary>
        public int FreeCapacity => _buffer.Length - _index;

        /// <summary>
        /// Clears the data written to the underlying buffer.
        /// </summary>
        /// <remarks>
        /// You must clear the <see cref="ConsumedArrayBufferWriter{byte}"/> before trying to re-use it.
        /// </remarks>
        public void Clear()
        {
            Debug.Assert(_buffer.Length >= _index);
            _buffer.AsSpan(0, _index).Clear();
            _index = 0;
            _consumedCount = 0;
        }

        /// <summary>
        /// Notifies <see cref="IBufferWriter{byte}"/> that <paramref name="count"/> amount of data was written to the output <see cref="Span{byte}"/>/<see cref="Memory{byte}"/>
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="count"/> is negative.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when attempting to advance past the end of the underlying buffer.
        /// </exception>
        /// <remarks>
        /// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
        /// </remarks>
        public void Advance(int count)
        {
            if (count < 0)
                throw new ArgumentException(nameof(count));

            if (_index > _buffer.Length - count)
                throw new InvalidOperationException($"BufferWriter advanced too far. Capacity: {_buffer.Length}");

            _index += count;
        }

        /// <summary>
        /// Notifies <see cref="ConsumableArrayBufferWriter{byte}"/> that <paramref name="count"/> amount of data was consumed from the output <see cref="Span{byte}"/>/<see cref="Memory{byte}"/ and can be overwritten>
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="count"/> is negative.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when attempting to consume more data than has been written.
        /// </exception>
        /// <remarks>
        /// Do not access data after it has been consumed.
        /// </remarks>
        public void Consume(int count)
        {
            if (count < 0)
                throw new ArgumentException(nameof(count));

            if (_consumedCount > _index - count)
                throw new InvalidOperationException($"More data consumed from BufferWriter than was written.");

            var newConsumedCount = _consumedCount + count;
            if (newConsumedCount == _index)
            {
                _index = 0;
                _consumedCount = 0;
                if (Capacity >= DefaultInitialBufferSize * 2)
                {
                    // No point holding on to a large buffer
                    ArrayPool<byte>.Shared.Return(_buffer);
                    _buffer = Array.Empty<byte>();
                }
            }
            else
            {
                _consumedCount = newConsumedCount;
            }
        }

        /// <summary>
        /// Returns a <see cref="Memory{byte}"/> to write to that is at least the requested length (specified by <paramref name="sizeHint"/>).
        /// If no <paramref name="sizeHint"/> is provided (or it's equal to <code>0</code>), some non-empty buffer is returned.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="sizeHint"/> is negative.
        /// </exception>
        /// <remarks>
        /// This will never return an empty <see cref="Memory{byte}"/>.
        /// </remarks>
        /// <remarks>
        /// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.
        /// </remarks>
        /// <remarks>
        /// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
        /// </remarks>
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            Debug.Assert(_buffer.Length > _index);
            return _buffer.AsMemory(_index);
        }

        /// <summary>
        /// Returns a <see cref="Span{byte}"/> to write to that is at least the requested length (specified by <paramref name="sizeHint"/>).
        /// If no <paramref name="sizeHint"/> is provided (or it's equal to <code>0</code>), some non-empty buffer is returned.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="sizeHint"/> is negative.
        /// </exception>
        /// <remarks>
        /// This will never return an empty <see cref="Span{byte}"/>.
        /// </remarks>
        /// <remarks>
        /// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.
        /// </remarks>
        /// <remarks>
        /// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
        /// </remarks>
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            Debug.Assert(_buffer.Length > _index);
            return _buffer.AsSpan(_index);
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentException(nameof(sizeHint));

            if (sizeHint == 0)
            {
                sizeHint = 1;
            }


            if (sizeHint > FreeCapacity)
            {
                var countUnconsumed = _index - _consumedCount;
                var countAvailable = _buffer.Length - countUnconsumed;
                // If doing so would give us significant free capacity (half the array arbitrarily chosen here)
                // Shift the array left rather than allocating a new array.
                if (countAvailable > sizeHint && countAvailable > _buffer.Length / 2)
                {
                    Array.Copy(_buffer, _consumedCount, _buffer, 0, countUnconsumed);
                }
                else
                {
                    var growBy = Math.Max(sizeHint - _consumedCount, _buffer.Length);

                    if (_buffer.Length == 0)
                    {
                        growBy = Math.Max(growBy, DefaultInitialBufferSize);
                    }

                    var newSize = checked(_buffer.Length + growBy);
                    var destinationArray = ArrayPool<byte>.Shared.Rent(newSize);
                    Array.Copy(_buffer, _consumedCount, destinationArray, 0, countUnconsumed);
                    ReturnBuffer();
                    _buffer = destinationArray;
                }
                _index = countUnconsumed;
                _consumedCount = 0;
            }

            Debug.Assert(FreeCapacity > 0 && FreeCapacity >= sizeHint);
        }

        public void Dispose()
        {
            ReturnBuffer();
            _buffer = null; // This will cause a NRE if we use after dispose rather than writing data into a random array
        }

        private void ReturnBuffer()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}
