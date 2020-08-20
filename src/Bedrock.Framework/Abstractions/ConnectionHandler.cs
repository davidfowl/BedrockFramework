using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Connections;
using System.Text;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public abstract class ConnectionHandler
    {
        public abstract Task OnConnectedAsync(Connection connection);
    }
}
