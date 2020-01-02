using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Protocols
{
    public class ContentLengthHttpBodyReader : IMessageReader<ReadOnlySequence<byte>>
    {
        private long _remaining;

        public ContentLengthHttpBodyReader(long contentLength)
        {
            _remaining = contentLength;
        }

        public bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out ReadOnlySequence<byte> message)
        {
            if (_remaining == 0)
            {
                consumed = input.Start;
                examined = input.End;
                message = default;
                return true;
            }

            var read = Math.Min(_remaining, input.Length);

            _remaining -= read;

            message = input.Slice(0, read);
            consumed = message.End;
            examined = consumed;

            return true;
        }
    }
}
