using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.BulkCiphers
{
    internal abstract class BulkCipherKey : IDisposable
    {
        public abstract Memory<byte> IV { get; }
        public abstract int BlockSize { get; }
        public abstract int TagSize { get; }
        public abstract void Init(KeyMode mode);
        public abstract int Update(ReadOnlySpan<byte> input, Span<byte> output);
        public abstract int Finish(ReadOnlySpan<byte> input, Span<byte> output);
        public abstract void AddAdditionalInfo(ref AdditionalInfo addInfo);
        public abstract int GetTag(Span<byte> span);
        public abstract void SetTag(ReadOnlySpan<byte> tagSpan);
        public abstract void Dispose();
    }
}
