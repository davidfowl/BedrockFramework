using System;
using System.Buffers;
using System.Net.Http;
using System.Runtime.InteropServices;
using Bedrock.Framework.Infrastructure;

namespace Bedrock.Framework.Protocols
{
    public class Http1RequestMessageWriter : IProtocolWriter<HttpRequestMessage>
    {
        private ReadOnlySpan<byte> Http11 => new byte[] { (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/', (byte)'1', (byte)'.', (byte)'1' };
        private ReadOnlySpan<byte> NewLine => new byte[] { (byte)'\r', (byte)'\n' };
        private ReadOnlySpan<byte> Space => new byte[] { (byte)' ' };

        public void WriteMessage(HttpRequestMessage message, IBufferWriter<byte> output)
        {
            var writer = new BufferWriter<IBufferWriter<byte>>(output);
            writer.WriteAsciiNoValidation(message.Method.Method);
            writer.Write(Space);
            writer.WriteAsciiNoValidation(message.RequestUri.PathAndQuery);
            writer.Write(Space);
            writer.Write(Http11);
            writer.Write(NewLine);

            var colon = (byte)':';

            foreach (var header in message.Headers)
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
    }
}
