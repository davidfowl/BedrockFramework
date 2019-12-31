using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Bedrock.Framework.Protocols
{
    internal class HttpBodyContent : HttpContent
    {
        private Stream _stream;

        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromResult(_stream);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return _stream.CopyToAsync(stream);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        internal void SetStream(Stream stream)
        {
            _stream = stream;   
        }
    }
}
