using System.Buffers;

namespace Bedrock.Framework.Protocols
{
    public interface IMessageWriter<TMessage>
    {
        void WriteMessage(TMessage message, IBufferWriter<byte> output);
    }
}
