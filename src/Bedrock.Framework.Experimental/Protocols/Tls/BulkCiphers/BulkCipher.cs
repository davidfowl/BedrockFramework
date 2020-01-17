﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.BulkCiphers
{
    internal abstract class BulkCipher : IDisposable
    {
        protected const int AdditionalInfoHeaderSize = 13;
        protected ulong _sequenceNumber;
        protected BulkCipherKey _key;
        protected int _blockShifter;

        public int Overhead => _key.TagSize + sizeof(ulong);

        public abstract void Decrypt(PipeWriter pipeWriter, ReadOnlySequence<byte> cipherText, TlsFrameType recordType, TlsProtocolVersion tlsVersion);
        public abstract void Encrypt(PipeWriter pipeWriter, ReadOnlySpan<byte> plainText, TlsFrameType recordType);
        public abstract void Encrypt(PipeWriter pipeWriter, ReadOnlySequence<byte> plainText, TlsFrameType recordType);

        public void SetKey(BulkCipherKey key)
        {
            _key = key;
            _blockShifter = (int)Math.Log(_key.BlockSize, 2);
        }

        public virtual void IncrementSequence() => _sequenceNumber++;

        protected int WriteTag(ref PipeWriter writer)
        {
            var span = writer.GetSpan(_key.TagSize);
            _key.GetTag(span);
            writer.Advance(span.Length);
            return _key.TagSize;
        }

        public void Dispose()
        {
            _key?.Dispose();
            _key = null;
            GC.SuppressFinalize(this);
        }

        ~BulkCipher() => Dispose();
    }
}
