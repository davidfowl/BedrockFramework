using System.Net;

namespace Bedrock.Framework.Transports.Memory
{
    public class MemoryEndPoint : EndPoint
    {
        public static readonly MemoryEndPoint Default = new MemoryEndPoint("default");

        public MemoryEndPoint(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString() => Name;

        public override int GetHashCode() => Name.GetHashCode();
    }
}
