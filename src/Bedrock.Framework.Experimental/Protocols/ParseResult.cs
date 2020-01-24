using System;
using System.Runtime.CompilerServices;

namespace Bedrock.Framework.Protocols.Http.Http1
{
    public struct ParseResult<T>
    {
        private T _value;
        private Exception _error;

        public ParseResult(T value) : this()
        {
            _value = value;
        }

        public ParseResult(Exception error) : this()
        {
            _error = error;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(out T value)
        {
            value = _value;
            return _error is null;
        }

        public bool TryGetError(out Exception error)
        {
            error = _error;
            return error is object;
        }
    }
}
