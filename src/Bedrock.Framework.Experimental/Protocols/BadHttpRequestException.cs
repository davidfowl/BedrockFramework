using System.IO;

namespace Bedrock.Framework.Protocols.Http.Http1
{
    public class BadHttpRequestException : IOException
    {
        public BadHttpRequestException(RequestRejectionReason reason, string line) : base($"BadHttpRequest. Reason: {reason}, Line: {line}")
        {
            Reason = reason;
            Line = line;
        }

        public RequestRejectionReason Reason { get; }
        public string Line { get; }
    }
}