using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Runtime.CompilerServices;

namespace Bedrock.Framework.Protocols
{
    public class ChunkedHttpBodyReader : IHttpBodyReader
    {
        private static ReadOnlySpan<byte> NewLine => new byte[] { (byte)'\r', (byte)'\n' };

        private int LastChunkRemaining { get; set; }

        public bool IsCompleted { get; private set; }

        public bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out ReadOnlySequence<byte> message)
        {
            var sequenceReader = new SequenceReader<byte>(input);
            message = default;
            examined = input.End;

            while (true)
            {
                // Do we need to continue reading a active chunk?
                if (LastChunkRemaining > 0)
                {
                    var bytesToRead = Math.Min(LastChunkRemaining, sequenceReader.Remaining);

                    message = input.Slice(sequenceReader.Position, bytesToRead);

                    LastChunkRemaining -= (int)bytesToRead;

                    sequenceReader.Advance(bytesToRead);

                    if (LastChunkRemaining > 0)
                    {
                        examined = sequenceReader.Position;
                        // We need to read more data
                        break;
                    }
                    else if (!TryParseCrlf(ref sequenceReader))
                    {
                        break;
                    }

                    examined = sequenceReader.Position;
                }
                else
                {
                    if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> chunkSizeText, NewLine))
                    {
                        // Don't have a full chunk yet
                        break;
                    }

                    if (!TryParseChunkPrefix(chunkSizeText, out int chunkSize))
                    {
                        throw new InvalidDataException();
                    }

                    LastChunkRemaining = chunkSize;

                    // The last chunk is always of size 0
                    if (chunkSize == 0)
                    {
                        // The Body should end with two NewLine
                        if (!TryParseCrlf(ref sequenceReader))
                        {
                            break;
                        }

                        examined = sequenceReader.Position;
                        IsCompleted = true;
                        break;
                    }
                }
            }

            consumed = sequenceReader.Position;

            return message.Length > 0;
        }

        private static bool TryParseCrlf(ref SequenceReader<byte> sequenceReader)
        {
            // Need at least 2 characters in the buffer to make a call
            if (sequenceReader.Remaining < 2)
            {
                return false;
            }

            // We expect a crlf
            if (sequenceReader.IsNext(NewLine, advancePast: true))
            {
                return true;
            }

            // Didn't see that, broken server
            throw new InvalidDataException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseChunkPrefix(in ReadOnlySpan<byte> chunkSizeText, out int chunkSize)
        {
            return Utf8Parser.TryParse(chunkSizeText, out chunkSize, out _, 'x');
        }
    }
}
