using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServerApplication
{
    public interface IHttpApplication
    {
        Task ProcessRequests(IAsyncEnumerable<IHttpContext> requests);
    }
}
