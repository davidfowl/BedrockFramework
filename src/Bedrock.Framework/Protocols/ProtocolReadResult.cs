using System.Diagnostics.CodeAnalysis;

namespace Bedrock.Framework.Protocols
{
    public readonly struct ProtocolReadResult<TMessage>
    {
        public ProtocolReadResult([AllowNull] TMessage message, bool isCanceled, bool isCompleted)
        {
            Message = message;
            IsCanceled = isCanceled;
            IsCompleted = isCompleted;
        }

        [MaybeNull]
        public TMessage Message { get; }
        public bool IsCanceled { get; }
        public bool IsCompleted { get; }
    }
}
