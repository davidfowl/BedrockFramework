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
        private bool _hasMessage;
        // private ArrayBufferWriter<byte> _backlog = new ArrayBufferWriter<byte>();

        public MessagePipeReader(PipeReader reader, IMessageReader<ReadOnlySequence<byte>> messageReader)
        {
            _reader = reader;
            _messageReader = messageReader;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            AdvanceTo(consumed, consumed);
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            //var consumedBytes = _message.Slice(_message.Start, consumed).Length;

            //if (consumedBytes > _message.Length)
            //{
            //    _backlog.Clear();
            //}
            //else
            //{
            //    var unconsumed = _message.Slice(consumed);

            //    foreach (var m in unconsumed)
            //    {
            //        _backlog.Write(m.Span);
            //    }
            //}

            //// REVIEW: Use the correct value for examined
            //_reader.AdvanceTo(_consumed, _examined);
        }

        public override void CancelPendingRead()
        {
            _reader.CancelPendingRead();
        }

        public override void Complete(Exception exception = null)
        {

        }

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);

                var buffer = result.Buffer;
                _isCanceled = result.IsCanceled;
                _isCompleted = result.IsCompleted;

                if (_isCanceled)
                {
                    break;
                }

                if (_messageReader.TryParseMessage(buffer, out _consumed, out _examined, out _message))
                {
                    //if (_backlog.WrittenCount > 0)
                    //{
                    //    foreach (var m in _message)
                    //    {
                    //        _backlog.Write(m.Span);
                    //    }

                    //    return new ReadResult(new ReadOnlySequence<byte>(_backlog.WrittenMemory), _isCanceled, _isCompleted);
                    //}

                    _hasMessage = true;
                    return new ReadResult(_message, _isCanceled, _isCompleted);
                }
                else
                {
                    _reader.AdvanceTo(_consumed, _examined);
                }

                if (_isCompleted)
                {
                    if (!buffer.IsEmpty)
                    {
                        throw new InvalidDataException("Connection terminated while reading a message.");
                    }

                    break;
                }
            }

            return new ReadResult(default, _isCanceled, _isCompleted);
        }

        public override bool TryRead(out ReadResult readResult)
        {
            readResult = default;
            return false;
        }
    }
}
