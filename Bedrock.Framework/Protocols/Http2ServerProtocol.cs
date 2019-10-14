using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Protocols.Http2;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class Http2ServerProtocol : Http2Protocol
    {
        private readonly Http2PeerSettings _serverSettings = new Http2PeerSettings();
        private readonly Http2PeerSettings _clientSettings = new Http2PeerSettings();

        public Http2ServerProtocol(ConnectionContext connection) : base(connection)
        {
        }

        public static Http2Protocol CreateFromConnection(ConnectionContext connection)
        {
            return new Http2Protocol(connection);
        }

        protected override async ValueTask ProcessFramesAsync()
        {
            if (!await TryReadPrefaceAsync())
            {
                return;
            }


            //if (_isClosed == 0)
            //{
            //    await FrameWriter.WriteSettingsAsync(_serverSettings.GetNonProtocolDefaults());
            //    Inform the client that the connection window is larger than the default.It can't be lowered here,
            //     It can only be lowered by not issuing window updates after data is received.
            //    var connectionWindow = _context.ServiceContext.ServerOptions.Limits.Http2.InitialConnectionWindowSize;
            //    var diff = connectionWindow - (int)Http2PeerSettings.DefaultInitialWindowSize;
            //    if (diff > 0)
            //    {
            //        await _frameWriter.WriteWindowUpdateAsync(0, diff);
            //    }
            //}

            await base.ProcessFramesAsync();
        }



        private async Task<bool> TryReadPrefaceAsync()
        {
            var input = _connection.Transport.Input;

            while (true)
            {
                var result = await input.ReadAsync();
                var readableBuffer = result.Buffer;
                var consumed = readableBuffer.Start;
                var examined = readableBuffer.End;

                try
                {
                    if (TryParsePreface(readableBuffer, out consumed, out examined))
                    {
                        return true;
                    }

                    if (result.IsCompleted)
                    {
                        return false;
                    }
                }
                finally
                {
                    input.AdvanceTo(consumed, examined);
                }
            }
        }

        private bool TryParsePreface(in ReadOnlySequence<byte> buffer, out SequencePosition consumed, out SequencePosition examined)
        {
            consumed = buffer.Start;
            examined = buffer.End;

            if (buffer.Length < ClientPreface.Length)
            {
                return false;
            }

            var preface = buffer.Slice(0, ClientPreface.Length);
            var span = preface.ToSpan();

            if (!span.SequenceEqual(ClientPreface))
            {
                throw new Http2ConnectionErrorException("CoreStrings.Http2ErrorInvalidPreface", Http2ErrorCode.PROTOCOL_ERROR);
            }

            consumed = examined = preface.End;
            return true;
        }
    }
}
