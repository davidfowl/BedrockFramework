using System;
using System.Buffers;
using System.Net.Http;
using System.Text;

namespace Bedrock.Framework.Protocols
{
    public class Http1RequestMessageReader : IMessageReader<HttpRequestMessage>
    {
        private ReadOnlySpan<byte> NewLine => new byte[] { (byte)'\r', (byte)'\n' };
        private ReadOnlySpan<byte> TrimChars => new byte[] { (byte)' ', (byte)'\t' };

        private HttpRequestMessage _httpRequestMessage = new HttpRequestMessage();

        private State _state;

        public Http1RequestMessageReader(HttpContent content)
        {
            _httpRequestMessage.Content = content;
        }

        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out HttpRequestMessage message)
        {
            var sequenceReader = new SequenceReader<byte>(input);
            message = null;

            switch (_state)
            {
                case State.StartLine:
                    if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> method, (byte)' '))
                    {
                        return false;
                    }

                    if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> path, (byte)' '))
                    {
                        return false;
                    }

                    if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> version, NewLine))
                    {
                        return false;
                    }

                    _httpRequestMessage.Method = new HttpMethod(Encoding.ASCII.GetString(method));
                    _httpRequestMessage.RequestUri = new Uri(Encoding.ASCII.GetString(path), UriKind.Relative);
                    _httpRequestMessage.Version = new Version(1, 1);
                    // Version = Encoding.ASCII.GetString(version.IsSingleSegment ? version.FirstSpan : version.ToArray());

                    _state = State.Headers;

                    consumed = sequenceReader.Position;

                    goto case State.Headers;

                case State.Headers:
                    while (sequenceReader.TryReadTo(out ReadOnlySequence<byte> headerLine, NewLine))
                    {
                        if (headerLine.Length == 0)
                        {
                            consumed = sequenceReader.Position;
                            examined = consumed;

                            message = _httpRequestMessage;

                            // End of headers
                            _state = State.Body;
                            break;
                        }

                        // Parse the header
                        ParseHeader(headerLine, out var headerName, out var headerValue);

                        var key = Encoding.ASCII.GetString(headerName.Trim(TrimChars));
                        var value = Encoding.ASCII.GetString(headerValue.Trim(TrimChars));

                        if (!_httpRequestMessage.Headers.TryAddWithoutValidation(key, value))
                        {
                            _httpRequestMessage.Content.Headers.TryAddWithoutValidation(key, value);
                        }

                        consumed = sequenceReader.Position;
                    }
                    break;
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
