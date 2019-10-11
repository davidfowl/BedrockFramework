using System;
using System.Buffers;
using System.Net.Http;
using System.Runtime.InteropServices;
using Bedrock.Framework.Infrastructure;

namespace Bedrock.Framework.Protocols
{
    public class Http1ResponseMessageWriter : IProtocolWriter<HttpResponseMessage>
    {
        private ReadOnlySpan<byte> NewLine => new byte[] { (byte)'\r', (byte)'\n' };
        private ReadOnlySpan<byte> Space => new byte[] { (byte)' ' };

        public void WriteMessage(HttpResponseMessage message, IBufferWriter<byte> output)
        {
            var writer = new BufferWriter<IBufferWriter<byte>>(output);
            writer.WriteAsciiNoValidation("HTTP/1.1");
            writer.Write(Space);
            writer.WriteNumeric((uint)message.StatusCode);
            writer.Write(Space);
            writer.WriteAsciiNoValidation(message.StatusCode.ToString());
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
