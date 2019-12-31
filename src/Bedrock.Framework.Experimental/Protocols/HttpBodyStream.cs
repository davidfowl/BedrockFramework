using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Protocols
{
    internal class HttpBodyStream : Stream
    {
        private ProtocolReader _reader;
        private IHttpBodyReader _bodyReader;

        public HttpBodyStream(ProtocolReader reader, IHttpBodyReader bodyReader)
        {
            _reader = reader;
            _bodyReader = bodyReader;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new System.NotImplementedException();

        public override long Position { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public override void Flush()
        {
            throw new System.NotImplementedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_bodyReader.IsCompleted)
            {
                return 0;
            }

            var result = await _reader.ReadAsync(_bodyReader, maximumMessageSize: buffer.Length, cancellationToken).ConfigureAwait(false);

            if (result.IsCompleted)
            {
                return 0;
            }

            var data = result.Message;

            data.CopyTo(buffer.Span);

            _reader.Advance();

            return (int)data.Length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new System.NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }
    }
}