using System.Net;

namespace Bedrock.Framework.Transports.Memory;

public class MemoryEndPoint(string name) : EndPoint
{
    public static readonly MemoryEndPoint Default = new MemoryEndPoint("default");

    public string Name { get; } = name;

    public override string ToString() => Name;

    public override int GetHashCode() => Name.GetHashCode();
}
