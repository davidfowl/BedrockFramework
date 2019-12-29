using System.Net;

namespace Bedrock.Framework
{
    public class NamedPipeEndPoint : EndPoint
    {
        public NamedPipeEndPoint(string pipeName, string serverName = ".", System.IO.Pipes.PipeOptions pipeOptions = System.IO.Pipes.PipeOptions.Asynchronous)
        {
            ServerName = serverName;
            PipeName = pipeName;
            PipeOptions = pipeOptions;
        }

        public string ServerName { get; }
        public string PipeName { get; }
        public System.IO.Pipes.PipeOptions PipeOptions { get; set; }

        public override string ToString()
        {
            return $"Server = {ServerName}, Pipe = {PipeName}";
        }
    }
}
