using System.Buffers;

namespace Bedrock.Framework.Protocols
{
    public interface IProtocolWriter<TMessage>
    {
        void WriteMessage(TMessage message, IBufferWriter<byte> output);
    }
}
