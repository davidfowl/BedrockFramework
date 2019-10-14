using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework.Protocols.Http2;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class Http2ClientProtocol : Http2Protocol
    {
        private const int DefaultInitialWindowSize = 65535;

        // We don't really care about limiting control flow at the connection level.
        // We limit it per stream, and the user controls how many streams are created.
        // So set the connection window size to a large value.
        private const int ConnectionWindowSize = 64 * 1024 * 1024;

        private bool _expectingSettingsAck;

        public Http2ClientProtocol(ConnectionContext connection) : base(connection)
        {
            _ = ProcessFramesAsync();
        }

        public ValueTask<HttpResponseMessage> SendAsync(HttpRequestMessage requestMessage, HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead)
        {
            return default;
        }

        protected override async ValueTask ProcessFramesAsync()
        {
            _connection.Transport.Output.Write(ClientPreface);

            await FrameWriter.WriteSettingsAsync(new List<Http2PeerSetting>
            {
                // First setting: Disable push promise
                new Http2PeerSetting(Http2SettingsParameter.SETTINGS_ENABLE_PUSH, 0),
                // Second setting: Set header table size to 0 to disable dynamic header compression
                new Http2PeerSetting(Http2SettingsParameter.SETTINGS_HEADER_TABLE_SIZE, 0)
            });

            await FrameWriter.WriteWindowUpdateAsync(0, ConnectionWindowSize - DefaultInitialWindowSize);

            await FrameWriter.FlushAsync();

            _expectingSettingsAck = true;

            await base.ProcessFramesAsync();
        }
    }
}
