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
        public static byte[] ColonCr = new[] { COLON, CR };
        public static byte[] SpHt = new[] { SP, HT };
        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out ParseResult<Http1Header> message)
        {
            message = default;
            var reader = new SequenceReader<byte>(input);
            if (!reader.TryReadToAny(out ReadOnlySequence<byte> fieldName, ColonCr, advancePastDelimiter: false))
            {
                examined = input.End;
                return false;
            }

            reader.TryRead(out var delimiter);
            if (delimiter == CR)
            {
                examined = reader.Position;
                message = new ParseResult<Http1Header>(CreateException(RequestRejectionReason.InvalidRequestHeader, input, reader));
                return true;
            }

            reader.AdvancePastAny(SpHt);

            if (!reader.TryReadTo(out ReadOnlySequence<byte> fieldValue, CR, advancePastDelimiter: true))
            {
                examined = input.End;
                return false;
            }

            if (!reader.TryRead(out var final))
            {
                examined = input.End;
                return false;
            }

            if (final != LF)
            {
                examined = reader.Position;
                message = new ParseResult<Http1Header>(CreateException(RequestRejectionReason.InvalidRequestHeader, input, reader));
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

        public static BadHttpRequestException CreateException(RequestRejectionReason reason, ReadOnlySequence<byte> readOnlySequence, SequenceReader<byte> sequenceReader)
        {
            var line = Encoding.ASCII.GetString(readOnlySequence.Slice(readOnlySequence.Start, sequenceReader.Position).ToSpan());
            return new BadHttpRequestException(reason, line);
        }
    }
}
