using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Text;

namespace Bedrock.Framework.Protocols.Http.Http1
{
    public class Http1HeaderReader : IMessageReader<ParseResult<Http1Header>>
    {
        private const byte SP = (byte)' ';
        private const byte HT = (byte)'\t';
        private const byte COLON = (byte)':';
        private const byte CR = (byte)'\r';
        private const byte LF = (byte)'\n';

        private static ReadOnlySpan<byte> ColonOrWhitespace => new byte[] { COLON, CR, SP, HT };
        private static ReadOnlySpan<byte> SpOrHt => new byte[] { SP, HT };
        private static ReadOnlySpan<byte> CrOrLf => new byte[] { CR, LF };

        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out ParseResult<Http1Header> message)
        {
            message = default;
            var reader = new SequenceReader<byte>(input);
            if (!reader.TryReadToAny(out ReadOnlySequence<byte> fieldName, ColonOrWhitespace, advancePastDelimiter: false))
            {
                return false;
            }

            reader.TryRead(out var delimiter);
            if (delimiter != COLON || fieldName.IsEmpty)
            {
                examined = reader.Position;
                message = new ParseResult<Http1Header>(CreateError(input, reader));
                return true;
            }

            reader.AdvancePastAny(SpOrHt);

            if (!reader.TryReadToAny(out ReadOnlySequence<byte> fieldValue, CrOrLf, advancePastDelimiter: false))
            {
                return false;
            }

            reader.TryRead(out delimiter);
            if (delimiter != CR)
            {
                examined = reader.Position;
                message = new ParseResult<Http1Header>(CreateError(input, reader));
                return true;
            }

            if (!reader.TryRead(out var final))
            {
                return false;
            }

            if (final != LF)
            {
                examined = reader.Position;
                message = new ParseResult<Http1Header>(CreateError(input, reader));
                return true;
            }

            consumed = examined = reader.Position;

            var fieldValueMemory = fieldValue.ToMemory();
            var fieldValueSpan = fieldValueMemory.Span;
            var i = fieldValueSpan.Length - 1;
            for (; i >= 0; i--)
            {
                var b = fieldValueSpan[i];
                if (b == SP || b == HT)
                {
                    continue;
                }
                break;
            }
            message = new ParseResult<Http1Header>(new Http1Header(fieldName.ToMemory(), fieldValueMemory.Slice(0, i + 1)));
            return true;
        }

        public static ParseError CreateError(ReadOnlySequence<byte> readOnlySequence, SequenceReader<byte> sequenceReader)
        {
            var line = Encoding.ASCII.GetString(readOnlySequence.Slice(readOnlySequence.Start, sequenceReader.Position).ToSpan());
            return new ParseError("Invalid Http Header", line);
        }
    }
}
