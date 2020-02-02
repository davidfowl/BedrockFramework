using System.Runtime.CompilerServices;

namespace Bedrock.Framework.Protocols.Http.Http1
{
    public struct ParseResult<T>
    {
        private readonly T _value;
        private readonly ParseError _error;

        public ParseResult(T value) : this()
        {
            _value = value;
        }

        public ParseResult(ParseError error) : this()
        {
            _error = error;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(out T value)
        {
            value = _value;
            return _error is null;
        }

        public bool TryGetError(out ParseError error)
        {
            error = _error;
            return error is object;
        }
    }

    public class ParseError
    {
        public ParseError(string reason, string line)
        {
            Reason = reason;
            Line = line;
        }

        public string Reason { get; }
        public string Line { get; }
    }
}
