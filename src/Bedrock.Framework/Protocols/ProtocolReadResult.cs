namespace Bedrock.Framework.Protocols
{
    /// <summary>
    /// Represents a reading result from <see cref="ProtocolReader"/>.
    /// </summary>
    /// <typeparam name="TMessage">The read message.</typeparam>
    public readonly struct ProtocolReadResult<TMessage>
    {
        public ProtocolReadResult(TMessage message, bool isCanceled, bool isCompleted)
        {
            Message = message;
            IsCanceled = isCanceled;
            IsCompleted = isCompleted;
        }

        /// <summary>
        /// The read message.
        /// </summary>
        public TMessage Message { get; }

        /// <summary>
        /// Whether the reading operation was cancelled (true) or not (false).
        /// </summary>
        public bool IsCanceled { get; }

        /// <summary>
        /// Whether the reading operation was completed (true) or not (false). 
        /// </summary>
        public bool IsCompleted { get; }
    }
}
