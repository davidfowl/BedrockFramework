using System;
using System.IO.Pipes;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public class NamedPipeConnectionFactory : ConnectionFactory
    {
        public override async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            if (!(endPoint is NamedPipeEndPoint np))
            {
                throw new NotSupportedException($"{endPoint.GetType()} is not supported");
            }

            var pipeStream = new NamedPipeClientStream(np.ServerName, np.PipeName, PipeDirection.InOut, np.PipeOptions);
            await pipeStream.ConnectAsync(cancellationToken).ConfigureAwait(false);

            return Connection.FromStream(pipeStream, leaveOpen: false, localEndPoint: np, remoteEndPoint: np);
        }
    }
}
