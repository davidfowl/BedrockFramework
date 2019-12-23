using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DistributedApplication.ServiceRegistry
{
    public interface IServiceRegistryClient
    {
        Task Sync(IEnumerable<Server> servers);
        Task Join(Server server);
        Task Leave(Server server);
    }

    public class Server
    {
        public string Id { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }

        public override string ToString()
        {
            return $"{Id}@{Host}:{Port}";
        }
    }
}
