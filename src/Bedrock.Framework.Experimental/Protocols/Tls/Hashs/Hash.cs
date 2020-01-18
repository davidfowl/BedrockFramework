using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Hashs
{
    public abstract class Hash : IDisposable
    {
        public abstract void HashData(ReadOnlySpan<byte> data);
        public abstract int HashSize { get; }
        public abstract int InterimHash(Span<byte> output);
        public abstract int Finish(Span<byte> output);
        public abstract HashType HashType { get; }

        public void HashData(SequenceReader<byte> reader)
        {
            while (!reader.End)
            {
                HashData(reader.CurrentSpan);
                reader.Advance(reader.CurrentSpan.Length);
            }
        }

        public void HashData(ReadOnlySequence<byte> sequence) => HashData(new SequenceReader<byte>(sequence));

        protected abstract void Dispose(bool disposing);

        ~Hash() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
