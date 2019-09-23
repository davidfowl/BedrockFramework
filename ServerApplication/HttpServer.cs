using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ServerApplication
{
    public class HttpServer
    {
        private readonly Server _server;

        public HttpServer(IPAddress address, int port)
        {
            var socketOptions = Options.Create(new SocketTransportOptions());
            var sockets = new SocketTransportFactory(socketOptions, NullLoggerFactory.Instance);
            var options = new ServerOptions()
                   .Listen(new IPEndPoint(address, port), sockets, builder => builder.Run(connection => new HttpConnection(connection).RunAsync()));

            _server = new Server(NullLoggerFactory.Instance, options);
        }

        public Task StartAsync()
        {
            return _server.StartAsync(default);
        }

        public Task StopAsync()
        {
            return _server.StopAsync(default);
        }

        private class HttpConnection
        {
            private readonly ConnectionContext _connection;
            private State _state;

            private ReadOnlySpan<byte> NewLine => new byte[] { 13, 10 };

            public string Method { get; set; }
            public string Path { get; set; }
            public string Version { get; set; }

            private Dictionary<string, string[]> Headers { get; } = new Dictionary<string, string[]>();

            public HttpConnection(ConnectionContext connection)
            {
                _connection = connection;
            }

            internal async Task RunAsync()
            {
                while (true)
                {
                    var result = await _connection.Transport.Input.ReadAsync();
                    var buffer = result.Buffer;

                    ParseHttpRequest(ref buffer, out var examined);

                    _connection.Transport.Input.AdvanceTo(buffer.Start, examined);

                    if (_state == State.Body)
                    {
                        // We're done parsing the request, now parse the body (if there is a body)

                        // Write a the response
                        var responseData = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 11\r\n\r\nHello World");
                        await _connection.Transport.Output.WriteAsync(responseData);

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

                if (_state == State.Headers)
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

                        var key = Encoding.ASCII.GetString(headerName);
                        var value = Encoding.ASCII.GetString(headerValue);

                        if (Headers.TryGetValue(key, out var values))
                        {
                            Array.Resize(ref values, values.Length + 1);
                            values[^1] = value;
                        }
                        else
                        {
                            Headers[key] = new string[] { value };
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
}
