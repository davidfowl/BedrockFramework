namespace Bedrock.Framework.Protocols
{
    public readonly struct ProtocolReadResult<TMessage>
    {
        public ProtocolReadResult(TMessage message, bool isCanceled, bool isCompleted)
        {
            Message = message;
            IsCanceled = isCanceled;
            IsCompleted = isCompleted;
        }

        public TMessage Message { get; }
        public bool IsCanceled { get; }
        public bool IsCompleted { get; }
    }
}
