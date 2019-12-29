using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class HttpServerProtocol : IHttpContext
    {
        private readonly ConnectionContext _connection;
        private State _state;

        private ReadOnlySpan<byte> NewLine => new byte[] { (byte)'\r', (byte)'\n' };
        private ReadOnlySpan<byte> TrimChars => new byte[] { (byte)' ', (byte)'\t' };

        public string Method { get; set; }
        public string Path { get; set; }
        public string Version { get; set; }
        public IDictionary<string, object> RequestHeaders { get; } = new Dictionary<string, object>();
        public PipeReader Input => _connection.Transport.Input;
        public PipeWriter Output => _connection.Transport.Output;

        public async ValueTask ReadHeadersAsync()
        {
            while (_state == State.Headers)
            {
                var result = await _connection.Transport.Input.ReadAsync();
                var buffer = result.Buffer;

                ParseHttpRequest(ref buffer, out var examined);

                _connection.Transport.Input.AdvanceTo(buffer.Start, examined);
            }
        }

        internal HttpServerProtocol(ConnectionContext connection)
        {
            _connection = connection;
        }

        public static HttpServerProtocol CreateFromConnection(ConnectionContext connection)
        {
            return new HttpServerProtocol(connection);
        }

        public async IAsyncEnumerable<IHttpContext> ReadAllRequestsAsync()
        {
            while (true)
            {
                var result = await _connection.Transport.Input.ReadAsync();
                var buffer = result.Buffer;

                ParseHttpRequest(ref buffer, out var examined);

                _connection.Transport.Input.AdvanceTo(buffer.Start, examined);

                if (_state != State.StartLine)
                {
                    yield return this;

                    // Consume the headers (TODO Don't materialize them)
                    await ReadHeadersAsync();

                    // Consume the body here (This is more complicated as we need to read the correct body)

                    _state = State.StartLine;
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
        }

        private void ParseHttpRequest(ref ReadOnlySequence<byte> buffer, out SequencePosition examined)
        {
            var sequenceReader = new SequenceReader<byte>(buffer);
            examined = buffer.End;

            if (_state == State.StartLine)
            {
                if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> method, (byte)' '))
                {
                    return;
                }

                if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> path, (byte)' '))
                {
                    return;
                }

                if (!sequenceReader.TryReadTo(out ReadOnlySequence<byte> version, NewLine))
                {
                    return;
                }

                Method = Encoding.ASCII.GetString(method);
                Path = Encoding.ASCII.GetString(path);
                Version = Encoding.ASCII.GetString(version.IsSingleSegment ? version.FirstSpan : version.ToArray());

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

                    if (RequestHeaders.TryGetValue(key, out var values))
                    {
                        if (values is string[] array)
                        {
                            Array.Resize(ref array, array.Length + 1);
                            array[^1] = value;
                        }
                        else
                        {
                            RequestHeaders[key] = new[] { (string)values, value };
                        }
                    }
                    else
                    {
                        RequestHeaders[key] = value;
                    }
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

