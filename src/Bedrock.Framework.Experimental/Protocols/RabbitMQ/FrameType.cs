namespace Bedrock.Framework.Experimental.Protocols.RabbitMQ
{
    internal enum FrameType : byte
    {
        Method = 1,
        Header = 2,
        Body = 3,
        HeartBeat = 8,
        End = 206
    }
}
