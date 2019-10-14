using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class Http2ClientProtocol : Http2Protocol
    {
        public Http2ClientProtocol(ConnectionContext connection) : base(connection)
        {
        }

        public override ValueTask ProcessFramesAsync()
        {
            return base.ProcessFramesAsync();
        }
    }
}
