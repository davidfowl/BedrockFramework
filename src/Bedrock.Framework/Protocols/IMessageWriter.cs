using System.Buffers;

namespace Bedrock.Framework.Protocols
{
    public interface IMessageWriter<TMessage>
    {
        void WriteMessage(ref TMessage message, IBufferWriter<byte> output);
    }
}
