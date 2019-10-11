using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Bedrock.Framework.Protocols
{
    public interface IHttpContext
    {
        IDictionary<string, object> RequestHeaders { get; }
        ValueTask ReadHeadersAsync();
        string Method { get; set; }
        string Path { get; set; }
        string Version { get; set; }
        PipeReader Input { get; }
        PipeWriter Output { get; }
    }
}