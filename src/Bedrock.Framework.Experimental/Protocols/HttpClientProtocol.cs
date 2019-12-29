using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework.Infrastructure;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class HttpClientProtocol
    {
        private readonly ConnectionContext _connection;
        private State _state;

        private ReadOnlySpan<byte> Http11 => new byte[] { (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/', (byte)'1', (byte)'.', (byte)'1' };
        private ReadOnlySpan<byte> NewLine => new byte[] { (byte)'\r', (byte)'\n' };
        private ReadOnlySpan<byte> Space => new byte[] { (byte)' ' };
        private ReadOnlySpan<byte> TrimChars => new byte[] { (byte)' ', (byte)'\t' };

        private HttpClientProtocol(ConnectionContext connection)
        {
            _connection = connection;
        }

        public async ValueTask<HttpResponseMessage> SendAsync(HttpRequestMessage requestMessage, HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead)
        {
            WriteHttpRequestMessage(requestMessage);

            if (requestMessage.Content != null)
            {
                await requestMessage.Content.CopyToAsync(_connection.Transport.Output.AsStream()).ConfigureAwait(false);
            }

            await _connection.Transport.Output.FlushAsync();

            var response = new HttpResponseMessage();

            while (true)
            {
                var result = await _connection.Transport.Input.ReadAsync().ConfigureAwait(false);
                var buffer = result.Buffer;

                ParseHttpResponse(ref buffer, response, out var examined);

                _connection.Transport.Input.AdvanceTo(buffer.Start, examined);

                if (_state == State.Body)
                {
                    break;
                }

                if (result.IsCompleted)
                {
                    if (_state != State.Body)
                    {
                        // Incomplete request, close the connection with an error
                    }
                    break;
                }
            }

            return response;
        }

        private void WriteHttpRequestMessage(HttpRequestMessage requestMessage)
        {
            var writer = new BufferWriter<PipeWriter>(_connection.Transport.Output);
            writer.WriteAsciiNoValidation(requestMessage.Method.Method);
            writer.Write(Space);
            writer.WriteAsciiNoValidation(requestMessage.RequestUri.ToString());
            writer.Write(Space);
            writer.Write(Http11);
            writer.Write(NewLine);

            var colon = (byte)':';

            foreach (var header in requestMessage.Headers)
            {
                foreach (var value in header.Value)
                {
                    writer.WriteAsciiNoValidation(header.Key);
                    writer.Write(MemoryMarshal.CreateReadOnlySpan(ref colon, 1));
                    writer.Write(Space);
                    writer.WriteAsciiNoValidation(value);
                    writer.Write(NewLine);
                }
            }

            writer.Write(NewLine);
            writer.Commit();
        }

        public static HttpClientProtocol CreateFromConnection(ConnectionContext connection)
        {
            return new HttpClientProtocol(connection);
        }

        private void ParseHttpResponse(ref ReadOnlySequence<byte> buffer, HttpResponseMessage httpResponse, out SequencePosition examined)
        {
            var sequenceReader = new SequenceReader<byte>(buffer);
            examined = buffer.End;

            if (_state == State.StartLine)
            {
                if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> version, (byte)' '))
                {
                    return;
                }

                if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> statusCodeText, (byte)' '))
                {
                    return;
                }

                if (!sequenceReader.TryReadTo(out ReadOnlySequence<byte> statusText, NewLine))
                {
                    return;
                }

                Utf8Parser.TryParse(statusCodeText, out int statusCode, out _);

                httpResponse.StatusCode = (HttpStatusCode)statusCode;
                httpResponse.ReasonPhrase = Encoding.ASCII.GetString(statusText.IsSingleSegment ? statusText.FirstSpan : statusText.ToArray());
                httpResponse.Version = new Version(1, 1); // TODO: Check

                _state = State.Headers;
                examined = sequenceReader.Position;
            }
            else if (_state == State.Headers)
            {
                while (sequenceReader.TryReadTo(out var headerLine, NewLine))
                {
                    if (headerLine.Length == 0)
                    {
                        examined = sequenceReader.Position;
                        // End of headers
                        _state = State.Body;
                        break;
                    }

                    // Parse the header
                    ParseHeader(headerLine, out var headerName, out var headerValue);

                    var key = Encoding.ASCII.GetString(headerName.Trim(TrimChars));
                    var value = Encoding.ASCII.GetString(headerValue.Trim(TrimChars));

                    httpResponse.Headers.TryAddWithoutValidation(key, value);
                }
            }

            // Slice whatever we've read so far
            buffer = buffer.Slice(sequenceReader.Position);
        }

        private static void ParseHeader(in ReadOnlySequence<byte> headerLine, out ReadOnlySpan<byte> headerName, out ReadOnlySpan<byte> headerValue)
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
