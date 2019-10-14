using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework.Protocols.Http2;
using Bedrock.Framework.Protocols.Http2.FlowControl;
using Bedrock.Framework.Protocols.Http2.HPack;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bedrock.Framework.Protocols
{
    public class Http2Protocol
    {
        public static byte[] ClientPreface { get; } = Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");

        public const uint DefaultHeaderTableSize = 4096;
        public const bool DefaultEnablePush = true;
        public const uint DefaultMaxConcurrentStreams = uint.MaxValue;
        public const uint DefaultInitialWindowSize = 65535;
        public const uint DefaultMaxFrameSize = MinAllowedMaxFrameSize;
        public const uint DefaultMaxHeaderListSize = uint.MaxValue;
        public const uint MaxWindowSize = int.MaxValue;
        internal const int MinAllowedMaxFrameSize = 16 * 1024;
        internal const int MaxAllowedMaxFrameSize = 16 * 1024 * 1024 - 1;

        protected readonly ConnectionContext _connection;
        private protected readonly Http2Frame _incomingFrame = new Http2Frame();
        private protected readonly Http2FrameWriter _httpFrameWriter;
        private readonly OutputFlowControl _outputFlowControl = new OutputFlowControl(Http2PeerSettings.DefaultInitialWindowSize);
        private readonly HPackDecoder _hpackDecoder;

        private int _maxStreamsPerConnection = 100;
        private int _headerTableSize = (int)Http2PeerSettings.DefaultHeaderTableSize;
        private int _maxFrameSize = (int)Http2PeerSettings.DefaultMaxFrameSize;
        private int _maxRequestHeaderFieldSize = (int)Http2PeerSettings.DefaultMaxFrameSize;
        private int _initialConnectionWindowSize = 1024 * 128; // Larger than the default 64kb, and larger than any one single stream.
        private int _initialStreamWindowSize = 1024 * 96; // Larger than the default 64kb

        public Http2Protocol(ConnectionContext connection)
        {
            _connection = connection;
            _httpFrameWriter = new Http2FrameWriter(connection, _outputFlowControl, NullLogger.Instance);
            _hpackDecoder = new HPackDecoder(_headerTableSize, _maxRequestHeaderFieldSize);
        }

        private protected Http2FrameWriter FrameWriter => _httpFrameWriter;
        private protected Http2Frame FrameHeader => _incomingFrame;

        protected virtual async ValueTask ProcessFramesAsync()
        {
            try
            {
                var input = _connection.Transport.Input;

                while (true)
                {
                    var result = await input.ReadAsync();
                    var buffer = result.Buffer;

                    // Call UpdateCompletedStreams() prior to frame processing in order to remove any streams that have exceded their drain timeouts.
                    // UpdateCompletedStreams();

                    try
                    {
                        while (Http2FrameReader.TryReadFrame(ref buffer, _incomingFrame, MinAllowedMaxFrameSize, out var framePayload))
                        {
                            // Log.Http2FrameReceived(ConnectionId, _incomingFrame);
                            await ProcessFrameAsync(framePayload);
                        }

                        if (result.IsCompleted)
                        {
                            return;
                        }
                    }
                    //catch (Http2StreamErrorException ex)
                    //{
                    //    Log.Http2StreamError(ConnectionId, ex);
                    //    // The client doesn't know this error is coming, allow draining additional frames for now.
                    //    AbortStream(_incomingFrame.StreamId, new IOException(ex.Message, ex));

                    //    await _frameWriter.WriteRstStreamAsync(ex.StreamId, ex.ErrorCode);
                    //}
                    finally
                    {
                        input.AdvanceTo(buffer.Start, buffer.End);

                        // UpdateConnectionState();
                    }
                }
            }
            catch (ConnectionResetException ex)
            {
                // Don't log ECONNRESET errors when there are no active streams on the connection. Browsers like IE will reset connections regularly.
                //if (_clientActiveStreamCount > 0)
                //{
                //    Log.RequestProcessingError(ConnectionId, ex);
                //}

                //error = ex;
            }
        }

        private Task ProcessFrameAsync(in ReadOnlySequence<byte> payload)
        {
            // http://httpwg.org/specs/rfc7540.html#rfc.section.5.1.1
            // Streams initiated by a client MUST use odd-numbered stream identifiers; ...
            // An endpoint that receives an unexpected stream identifier MUST respond with
            // a connection error (Section 5.4.1) of type PROTOCOL_ERROR.
            if (_incomingFrame.StreamId != 0 && (_incomingFrame.StreamId & 1) == 0)
            {
                throw new Http2ConnectionErrorException("CoreStrings.FormatHttp2ErrorStreamIdEven(_incomingFrame.Type, _incomingFrame.StreamId)", Http2ErrorCode.PROTOCOL_ERROR);
            }

            return _incomingFrame.Type switch
            {
                Http2FrameType.DATA => ProcessDataFrameAsync(payload),
                Http2FrameType.HEADERS => ProcessHeadersFrameAsync(payload),
                Http2FrameType.PRIORITY => ProcessPriorityFrameAsync(),
                Http2FrameType.RST_STREAM => ProcessRstStreamFrameAsync(),
                Http2FrameType.SETTINGS => ProcessSettingsFrameAsync(payload),
                Http2FrameType.PUSH_PROMISE => throw new Http2ConnectionErrorException("CoreStrings.Http2ErrorPushPromiseReceived", Http2ErrorCode.PROTOCOL_ERROR),
                Http2FrameType.PING => ProcessPingFrameAsync(payload),
                Http2FrameType.GOAWAY => ProcessGoAwayFrameAsync(),
                Http2FrameType.WINDOW_UPDATE => ProcessWindowUpdateFrameAsync(),
                Http2FrameType.CONTINUATION => ProcessContinuationFrameAsync(payload),
                _ => ProcessUnknownFrameAsync(),
            };
        }

        protected virtual Task ProcessWindowUpdateFrameAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task ProcessContinuationFrameAsync(in ReadOnlySequence<byte> payload)
        {
            return Task.CompletedTask;
        }

        protected virtual Task ProcessGoAwayFrameAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task ProcessPingFrameAsync(in ReadOnlySequence<byte> payload)
        {
            if (_incomingFrame.StreamId != 0)
            {
                throw new Http2ConnectionErrorException("CoreStrings.FormatHttp2ErrorStreamIdNotZero(_incomingFrame.Type)", Http2ErrorCode.PROTOCOL_ERROR);
            }

            if (_incomingFrame.PayloadLength != 8)
            {
                throw new Http2ConnectionErrorException("CoreStrings.FormatHttp2ErrorUnexpectedFrameLength(_incomingFrame.Type, 8)", Http2ErrorCode.FRAME_SIZE_ERROR);
            }

            if (_incomingFrame.PingAck)
            {
                // TODO: verify that payload is equal to the outgoing PING frame
                return Task.CompletedTask;
            }

            return FrameWriter.WritePingAsync(Http2PingFrameFlags.ACK, payload).AsTask();
        }

        protected virtual Task ProcessSettingsFrameAsync(in ReadOnlySequence<byte> payload)
        {
            return Task.CompletedTask;
        }

        protected virtual Task ProcessUnknownFrameAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task ProcessRstStreamFrameAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task ProcessPriorityFrameAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task ProcessHeadersFrameAsync(in ReadOnlySequence<byte> payload)
        {
            return Task.CompletedTask;
        }

        protected virtual Task ProcessDataFrameAsync(in ReadOnlySequence<byte> payload)
        {
            return Task.CompletedTask;
        }
    }
}
