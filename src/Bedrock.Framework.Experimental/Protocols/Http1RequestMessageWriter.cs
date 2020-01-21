using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using Bedrock.Framework.Infrastructure;

namespace Bedrock.Framework.Protocols
{
    public class Http1RequestMessageWriter : IMessageWriter<HttpRequestMessage>
    {
        private ReadOnlySpan<byte> Http11 => new byte[] { (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/', (byte)'1', (byte)'.', (byte)'1' };
        private ReadOnlySpan<byte> NewLine => new byte[] { (byte)'\r', (byte)'\n' };
        private ReadOnlySpan<byte> Space => new byte[] { (byte)' ' };
        private ReadOnlySpan<byte> Colon => new byte[] { (byte)':' };
        private ReadOnlySpan<byte> Host => new byte[] { (byte)'H', (byte)'o', (byte)'s', (byte)'t' };

        private const int DefaultHttpPort = 80;
        private const int DefaultHttpsPort = 443;

        private readonly string _host;
        private readonly int _port;
        private readonly byte[] _hostHeaderValueBytes;

        public Http1RequestMessageWriter(string host, int port)
        {
            _host = host;
            _port = port;

            // Precalculate ASCII bytes for Host header
            string hostHeader = $"{_host}:{_port}";
            _hostHeaderValueBytes = Encoding.ASCII.GetBytes(hostHeader);
        }

        public void WriteMessage(HttpRequestMessage message, IBufferWriter<byte> output)
        {
            Debug.Assert(message.Method != null);
            Debug.Assert(message.RequestUri != null);

            var writer = new BufferWriter<IBufferWriter<byte>>(output);
            writer.WriteAsciiNoValidation(message.Method.Method);
            writer.Write(Space);
            // REVIEW: This isn't right
            writer.WriteAsciiNoValidation(message.RequestUri.ToString());
            writer.Write(Space);
            writer.Write(Http11);
            writer.Write(NewLine);

            if (message.Headers == null || message.Headers.Host == null)
            {
                writer.Write(Host);
                writer.Write(Colon);
                writer.Write(Space);
                writer.Write(_hostHeaderValueBytes);
                writer.Write(NewLine);
            }

            foreach (var header in message.Headers)
            {
                foreach (var value in header.Value)
                {
                    writer.WriteAsciiNoValidation(header.Key);
                    writer.Write(Colon);
                    writer.Write(Space);
                    writer.WriteAsciiNoValidation(value);
                    writer.Write(NewLine);
                }
            }

            if (message.Content != null)
            {
                foreach (var header in message.Content.Headers)
                {
                    foreach (var value in header.Value)
                    {
                        writer.WriteAsciiNoValidation(header.Key);
                        writer.Write(Colon);
                        writer.Write(Space);
                        writer.WriteAsciiNoValidation(value);
                        writer.Write(NewLine);
                    }
                }
            }

            writer.Write(NewLine);
            writer.Commit();
        }
    }
}
