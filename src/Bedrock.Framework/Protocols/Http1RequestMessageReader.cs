using System;
using System.Buffers;
using System.Net.Http;
using System.Text;

namespace Bedrock.Framework.Protocols
{
    public class Http1RequestMessageReader : IProtocolReader<HttpRequestMessage>
    {
        private ReadOnlySpan<byte> NewLine => new byte[] { (byte)'\r', (byte)'\n' };
        private ReadOnlySpan<byte> TrimChars => new byte[] { (byte)' ', (byte)'\t' };

        private HttpRequestMessage HttpRequestMessage = new HttpRequestMessage();

        private State _state;

        public bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out HttpRequestMessage message)
        {
            var sequenceReader = new SequenceReader<byte>(input);
            message = null;
            consumed = input.Start;
            examined = input.End;

            if (_state == State.StartLine)
            {
                if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> method, (byte)' '))
                {
                    return false;
                }

                if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> path, (byte)' '))
                {
                    return false;
                }

                if (!sequenceReader.TryReadTo(out ReadOnlySequence<byte> version, NewLine))
                {
                    return false;
                }

                HttpRequestMessage.Method = new HttpMethod(Encoding.ASCII.GetString(method));
                HttpRequestMessage.RequestUri = new Uri(Encoding.ASCII.GetString(path));
                HttpRequestMessage.Version = new Version(1, 1);
                // Version = Encoding.ASCII.GetString(version.IsSingleSegment ? version.FirstSpan : version.ToArray());

                _state = State.Headers;

                consumed = sequenceReader.Position;
                examined = consumed;
            }
            else if (_state == State.Headers)
            {
                while (sequenceReader.TryReadTo(out var headerLine, NewLine))
                {
                    if (headerLine.Length == 0)
                    {
                        consumed = sequenceReader.Position;
                        examined = consumed;

                        message = HttpRequestMessage;
                        HttpRequestMessage = new HttpRequestMessage();

                        // End of headers
                        _state = State.Body;
                        break;
                    }

                    // Parse the header
                    ParseHeader(headerLine, out var headerName, out var headerValue);

                    var key = Encoding.ASCII.GetString(headerName.Trim(TrimChars));
                    var value = Encoding.ASCII.GetString(headerValue.Trim(TrimChars));

                    HttpRequestMessage.Headers.TryAddWithoutValidation(key, value);

                    consumed = sequenceReader.Position;
                }
            }

            return _state == State.Body;
        }

        internal static void ParseHeader(in ReadOnlySequence<byte> headerLine, out ReadOnlySpan<byte> headerName, out ReadOnlySpan<byte> headerValue)
        {
            if (headerLine.IsSingleSegment)
            {
                var span = headerLine.FirstSpan;
                var colon = span.IndexOf((byte)':');
                headerName = span.Slice(0, colon);
                headerValue = span.Slice(colon + 1);
            }
            else
            {
                var headerReader = new SequenceReader<byte>(headerLine);
                headerReader.TryReadTo(out headerName, (byte)':');
                var remaining = headerReader.Sequence.Slice(headerReader.Position);
                headerValue = remaining.IsSingleSegment ? remaining.FirstSpan : remaining.ToArray();
            }
        }

        private enum State
        {
            StartLine,
            Headers,
            Body
        }
    }
}
