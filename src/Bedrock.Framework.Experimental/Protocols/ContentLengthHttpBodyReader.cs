using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Protocols
{
    public class ContentLengthHttpBodyReader : IHttpBodyReader
    {
        private long _remaining;

        public bool IsCompleted { get; private set; }

        public ContentLengthHttpBodyReader(long contentLength)
        {
            _remaining = contentLength;
            IsCompleted = _remaining == 0;
        }

        public bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out ReadOnlySequence<byte> message)
        {
            var read = Math.Min(_remaining, input.Length);

            if (read == 0)
            {
                consumed = input.Start;
                examined = input.End;
                message = default;
                return false;
            }

            _remaining -= read;

            message = input.Slice(0, read);
            consumed = message.End;
            examined = consumed;

            IsCompleted = _remaining == 0;

            return true;
        }
    }
}
