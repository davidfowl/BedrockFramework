using Bedrock.Framework.Experimental.Protocols.Tls.Records;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.BulkCiphers
{
    internal abstract class AeadBulkCipher : IDisposable
    {
        protected const int AdditionalInfoHeaderSize = 13;
        protected ulong _sequenceNumber;
        protected ISymmetricalCipher _key;

        public int Overhead => _key.TagSize;

        public abstract void Decrypt(ref ReadOnlySequence<byte> messageBuffer, PipeWriter writer, RecordType recordType, TlsVersion tlsVersion);
        public abstract void Encrypt(ref PipeWriter writer, ReadOnlySequence<byte> plainText, RecordType recordType, TlsVersion tlsVersion);
        public void SetKey(ISymmetricalCipher key) => _key = key;
        public virtual void IncrementSequence() => _sequenceNumber++;

        protected void WriteTag(ref PipeWriter writer)
        {
            var tagBuffer = writer.GetSpan(_key.TagSize);
            _key.GetTag(tagBuffer.Slice(0, _key.TagSize));
            writer.Advance(_key.TagSize);
        }

        protected void Decrypt(ref ReadOnlySequence<byte> messageBuffer, PipeWriter writer)
        {
            if (messageBuffer.IsSingleSegment)
            {
                var writeBuffer = writer.GetSpan(messageBuffer.FirstSpan.Length);
                writer.Advance(_key.Finish(messageBuffer.FirstSpan, writeBuffer));
                IncrementSequence();
                return;
            }
            var bytesRemaining = messageBuffer.Length;
            foreach (var b in messageBuffer)
            {
                if (b.Length == 0) continue;
                var writeBuffer = writer.GetSpan(b.Length);
                bytesRemaining -= b.Length;
                if (bytesRemaining == 0)
                {
                    writer.Advance(_key.Finish(b.Span, writeBuffer));
                    break;
                }
                writer.Advance(_key.Update(b.Span,writeBuffer));
            }
            IncrementSequence();
        }

        public void Dispose()
        {
            _key?.Dispose();
            _key = null;
            GC.SuppressFinalize(this);
        }

        ~AeadBulkCipher() => Dispose();
    }
}
