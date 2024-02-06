using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Principal;

namespace Bedrock.Framework
{
    public class NamedPipeEndPoint : EndPoint
    {
        public NamedPipeEndPoint(string pipeName,
                                 string serverName = ".",
                                 System.IO.Pipes.PipeOptions pipeOptions = System.IO.Pipes.PipeOptions.WriteThrough | System.IO.Pipes.PipeOptions.Asynchronous,
                                 TokenImpersonationLevel impersonationLevel = TokenImpersonationLevel.Anonymous)
        {
            ServerName = serverName;
            PipeName = pipeName;
            PipeOptions = pipeOptions;
            ImpersonationLevel = impersonationLevel;
        }

        public string ServerName { get; }
        public string PipeName { get; }
        public System.IO.Pipes.PipeOptions PipeOptions { get; set; }
        public TokenImpersonationLevel ImpersonationLevel { get; set; }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is NamedPipeEndPoint other && other.ServerName == ServerName && other.PipeName == PipeName;
        }

        public override int GetHashCode()
        {
            return ServerName.GetHashCode() ^ PipeName.GetHashCode();
        }

        public override string ToString()
        {
            return $"Server = {ServerName}, Pipe = {PipeName}";
        }
    }
}
