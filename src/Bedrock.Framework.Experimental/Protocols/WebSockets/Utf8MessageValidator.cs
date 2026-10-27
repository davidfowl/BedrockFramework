using System;
using System.Buffers;
using System.Text;
using System.Text.Unicode;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets;

/// <summary>
/// Incrementally validates that a text message is well-formed UTF-8 (RFC 6455 section 8.1), even when
/// code points are split across frames or reads. Invalid data is detected as soon as it is seen.
/// </summary>
internal struct Utf8MessageValidator
{
    // At most 3 bytes of an incomplete 4 byte sequence are carried between chunks
    private uint _carry;
    private int _carryLength;

    public void Reset()
    {
        _carry = 0;
        _carryLength = 0;
    }

    /// <summary>
    /// Validates the next chunk of the message.
    /// </summary>
    /// <param name="data">The next bytes of the message.</param>
    /// <param name="endOfMessage">Whether this chunk ends the message.</param>
    /// <returns>False if the message is not valid UTF-8.</returns>
    public bool TryValidate(ReadOnlySpan<byte> data, bool endOfMessage)
    {
        if (_carryLength > 0)
        {
            Span<byte> scratch = stackalloc byte[4];
            for (var i = 0; i < _carryLength; i++)
            {
                scratch[i] = (byte)(_carry >> (8 * i));
            }

            var take = Math.Min(4 - _carryLength, data.Length);
            data[..take].CopyTo(scratch[_carryLength..]);
            var combined = scratch[..(_carryLength + take)];

            var status = Rune.DecodeFromUtf8(combined, out _, out var consumed);
            if (status == OperationStatus.Done)
            {
                data = data[(consumed - _carryLength)..];
                _carryLength = 0;
            }
            else if (status == OperationStatus.NeedMoreData)
            {
                // Still incomplete; everything available joined the carry
                SetCarry(combined);
                return !endOfMessage;
            }
            else
            {
                return false;
            }
        }

        if (Utf8.IsValid(data))
        {
            return true;
        }

        // The data is either invalid, or valid apart from an incomplete sequence at the very end.
        var tailStart = FindIncompleteTail(data);
        if (tailStart < 0 || !Utf8.IsValid(data[..tailStart]))
        {
            return false;
        }

        var tail = data[tailStart..];
        if (Rune.DecodeFromUtf8(tail, out _, out _) != OperationStatus.NeedMoreData)
        {
            return false;
        }

        SetCarry(tail);
        return !endOfMessage;
    }

    /// <summary>
    /// Whether a message that has ended left an incomplete sequence behind.
    /// </summary>
    public readonly bool IsComplete => _carryLength == 0;

    private void SetCarry(ReadOnlySpan<byte> bytes)
    {
        _carry = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            _carry |= (uint)bytes[i] << (8 * i);
        }
        _carryLength = bytes.Length;
    }

    /// <summary>
    /// Finds the start of the last lead byte if it begins a sequence that runs past the end of the data.
    /// </summary>
    private static int FindIncompleteTail(ReadOnlySpan<byte> data)
    {
        for (var i = 1; i <= 3 && i <= data.Length; i++)
        {
            var b = data[^i];
            if ((b & 0b1100_0000) == 0b1000_0000)
            {
                // Continuation byte, keep looking for the lead
                continue;
            }

            var expected = b >= 0xF0 ? 4 : b >= 0xE0 ? 3 : b >= 0xC0 ? 2 : 1;
            return expected > i ? data.Length - i : -1;
        }

        return -1;
    }
}
