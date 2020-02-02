using System;

namespace Bedrock.Framework.Protocols.Http.Http1
{
    public readonly struct Http1Header
    {
        private readonly ReadOnlyMemory<byte> _name;
        private readonly ReadOnlyMemory<byte> _value;

        public Http1Header(ReadOnlyMemory<byte> name, ReadOnlyMemory<byte> value)
        {
            _name = name;
            _value = value;
        }

        public ReadOnlySpan<byte> Name => _name.Span;
        public ReadOnlySpan<byte> Value => _value.Span;
    }
}
