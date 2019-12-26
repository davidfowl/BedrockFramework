namespace Bedrock.Framework.Protocols
{
    public readonly struct ReadResult<TMessage>
    {
        public ReadResult(TMessage message, bool isCanceled, bool isCompleted)
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
