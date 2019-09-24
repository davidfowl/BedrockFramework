using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServerApplication
{
    public interface IHttpApplication
    {
        Task ProcessRequest(IHttpContext connection);
    }
}
