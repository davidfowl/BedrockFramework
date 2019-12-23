using System.Collections.Generic;
using System.Threading.Tasks;
using Bedrock.Framework.Protocols;

namespace ServerApplication
{
    public interface IHttpApplication
    {
        Task ProcessRequests(IAsyncEnumerable<IHttpContext> requests);
    }
}
