﻿using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Bedrock.Framework.Protocols
{
    public class Http1ResponseMessageReader : IMessageReader<HttpResponseMessage>
    {
        private ReadOnlySpan<byte> NewLine => new byte[] { (byte)'\r', (byte)'\n' };
        private ReadOnlySpan<byte> TrimChars => new byte[] { (byte)' ', (byte)'\t' };

        private HttpResponseMessage _httpResponseMessage = new HttpResponseMessage();

        private State _state;

        public Http1ResponseMessageReader(HttpContent content)
        {
            _httpResponseMessage.Content = content;
        }

        public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out HttpResponseMessage message)
        {
            var sequenceReader = new SequenceReader<byte>(input);
            message = null;

            switch (_state)
            {
                case State.StartLine:
                    if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> version, (byte)' '))
                    {
                        return false;
                    }

                    if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> statusCodeText, (byte)' '))
                    {
                        return false;
                    }

                    if (!sequenceReader.TryReadTo(out ReadOnlySequence<byte> statusText, NewLine))
                    {
                        return false;
                    }

                    Utf8Parser.TryParse(statusCodeText, out int statusCode, out _);

                    _httpResponseMessage.StatusCode = (HttpStatusCode)statusCode;
                    var reasonPhrase = Encoding.ASCII.GetString(statusText.IsSingleSegment ? statusText.FirstSpan : statusText.ToArray());
                    _httpResponseMessage.ReasonPhrase = reasonPhrase;
                    _httpResponseMessage.Version = new Version(1, 1); // TODO: Check

                    _state = State.Headers;

                    consumed = sequenceReader.Position;
                    examined = consumed;

                    goto case State.Headers;

                case State.Headers:
                    while (sequenceReader.TryReadTo(out var headerLine, NewLine))
                    {
                        if (headerLine.Length == 0)
                        {
                            consumed = sequenceReader.Position;
                            examined = consumed;

                            message = _httpResponseMessage;

                            // End of headers
                            _state = State.Body;
                            break;
                        }

                        // Parse the header
                        Http1RequestMessageReader.ParseHeader(headerLine, out var headerName, out var headerValue);

                        var key = Encoding.ASCII.GetString(headerName.Trim(TrimChars));
                        var value = Encoding.ASCII.GetString(headerValue.Trim(TrimChars));

                        if (!_httpResponseMessage.Headers.TryAddWithoutValidation(key, value))
                        {
                            _httpResponseMessage.Content.Headers.TryAddWithoutValidation(key, value);
                        }

                        consumed = sequenceReader.Position;
                    }

                    examined = sequenceReader.Position;
                    break;
                default:
                    break;
            }

            return _state == State.Body;
        }

        private enum State
        {
            StartLine,
            Headers,
            Body
        }
    }
}
