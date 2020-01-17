using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.BulkCiphers
{
    internal interface ISymmetricalCipher : IDisposable
    {
        Memory<byte> IV { get; }
        int TagSize { get; }
        void Init(KeyMode mode);
        int Update(ReadOnlySpan<byte> input, Span<byte> output);
        int Update(Span<byte> inOutput);
        int Finish(Span<byte> inOuput);
        int Finish(ReadOnlySpan<byte> input, Span<byte> output);
        void AddAdditionalInfo(in AdditionalInfo addInfo);
        void GetTag(Span<byte> tagOutput);
        void SetTag(ReadOnlySpan<byte> tagInput);
    }
}
