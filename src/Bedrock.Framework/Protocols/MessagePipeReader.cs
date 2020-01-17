using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Protocols
{
    public class MessagePipeReader : PipeReader
    {
        private IMessageReader<ReadOnlySequence<byte>> _messageReader;
        private readonly PipeReader _reader;

        private SequencePosition _examined;
        private SequencePosition _consumed;
        private ReadOnlySequence<byte> _message;
        private bool _isCanceled;
        private bool _isCompleted;
        private ConsumableArrayBufferWriter<byte> _backlog = new ConsumableArrayBufferWriter<byte>();
        private bool _allExamined;

        public MessagePipeReader(PipeReader reader, IMessageReader<ReadOnlySequence<byte>> messageReader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _messageReader = messageReader ?? throw new ArgumentNullException(nameof(messageReader));
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            AdvanceTo(consumed, consumed);
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            if (examined.Equals(_message.End))
            {
                _allExamined = true;
            }
            
            var consumedLength = (int)_message.Slice(_message.Start, consumed).Length;

            if (_backlog.UnconsumedWrittenCount > 0)
            {
                _backlog.Consume(consumedLength);
            }
            else
            {
                var unconsumed = _message.Slice(consumed);
                if (!unconsumed.IsEmpty)
                {
                    foreach (var m in unconsumed)
                    {
                        _backlog.Write(m.Span);
                    }
                }
            }

            // REVIEW: Use the correct value for examined
            _reader.AdvanceTo(_consumed, _examined);
        }

        public override void CancelPendingRead()
        {
            _reader.CancelPendingRead();
        }

        public override void Complete(Exception exception = null)
        {
            _reader.Complete();
        }

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

                if (TryCreateReadResult(result, out var readResult))
                    return readResult;
            }
        }

        public override bool TryRead(out ReadResult readResult)
        {
            if (_reader.TryRead(out var result))
            {
                if (TryCreateReadResult(result, out readResult))
                    return true;
            }

            if (_allExamined)
            {
                readResult = default;
                return false;
            }

            _message = new ReadOnlySequence<byte>(_backlog.WrittenMemory);
            readResult = new ReadResult(_message, _isCanceled, _isCompleted);
            return true;
        }

        private bool TryCreateReadResult(ReadResult underlyingReadResult, out ReadResult readResult)
        {
            var buffer = underlyingReadResult.Buffer;
            _isCanceled = underlyingReadResult.IsCanceled;
            _isCompleted = underlyingReadResult.IsCompleted;

            if (_isCanceled)
            {
                readResult = new ReadResult(default, _isCanceled, _isCompleted);
                return true;
            }

            if (_messageReader.TryParseMessage(buffer, ref _consumed, ref _examined, out _message))
            {
                if (_backlog.UnconsumedWrittenCount > 0)
                {
                    foreach (var m in _message)
                    {
                        _backlog.Write(m.Span);
                    }

                    _message = new ReadOnlySequence<byte>(_backlog.WrittenMemory);
                }

                if (!_message.IsEmpty)
                    _allExamined = false;

                readResult = new ReadResult(_message, _isCanceled, _isCompleted);
                return true;
            }

            if (_isCompleted)
            {
                if (!buffer.IsEmpty)
                {
                    throw new InvalidDataException("Connection terminated while reading a message.");
                }

                _message = new ReadOnlySequence<byte>(_backlog.WrittenMemory);
                readResult = new ReadResult(_message, _isCanceled, _isCompleted);
                return true;
            }

            _reader.AdvanceTo(_consumed, _examined);

            readResult = default;
            return false;
        }
    }
}
