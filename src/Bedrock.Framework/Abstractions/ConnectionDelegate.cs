using System.Net.Connections;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public delegate Task ConnectionDelegate(Connection connection);
}
