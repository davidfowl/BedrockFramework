#nullable enable

using Bedrock.Framework.Experimental.Protocols.Kafka.Primitives;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public static class StrategyPayloadWriterExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StrategyPayloadWriter<TStrategy> WriteString<TStrategy>(this StrategyPayloadWriter<TStrategy> writer, string? value)
            where TStrategy : struct, IPayloadWriterStrategy
        {
            var length = value?.Length ?? -1;
            writer.Write((short)length);

            if (value != null)
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                writer.WriteBytes(ref bytes!);
            }

            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StrategyPayloadWriter<TStrategy> WriteBytes<TStrategy>(this StrategyPayloadWriter<TStrategy> writer, ref byte[]? bytes)
            where TStrategy : struct, IPayloadWriterStrategy
        {
            var length = bytes?.Length ?? -1;

            return writer.Write(bytes, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StrategyPayloadWriter<TStrategy> WriteBytes<TStrategy>(this StrategyPayloadWriter<TStrategy> writer, ref ReadOnlySpan<byte> bytes)
            where TStrategy : struct, IPayloadWriterStrategy
        {
            writer.Context.CurrentWriter.Write(bytes);

            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StrategyPayloadWriter<TStrategy> Write<TStrategy>(this StrategyPayloadWriter<TStrategy> writer, byte[]? bytes, int? length)
            where TStrategy : struct, IPayloadWriterStrategy
        {
            writer.Write(length ?? -1);

            if (bytes != null && length.HasValue)
            {
                var readOnlyBytes = new ReadOnlySpan<byte>(bytes, 0, length.Value);
                writer.WriteBytes(ref readOnlyBytes);
            }

            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StrategyPayloadWriter<TStrategy> WriteNullableString<TStrategy>(this StrategyPayloadWriter<TStrategy> writer, ref NullableString value)
            where TStrategy : struct, IPayloadWriterStrategy
        {
            writer.Write(value.Length);

            if (value.Length != -1)
            {
                writer.Write(value.Bytes.Span);
            }

            return writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StrategyPayloadWriter<TStrategy> WriteArray<T, TStrategy>(this StrategyPayloadWriter<TStrategy> writer, T[]? array, Func<T, StrategyPayloadWriterContext<TStrategy>, StrategyPayloadWriterContext<TStrategy>> action)
            where TStrategy : struct, IPayloadWriterStrategy
        {
            writer.Write(array?.Length ?? -1);

            for (int i = 0; i < array?.Length; i++)
            {
                var modifiedContext = action(array[i], writer.Context);
                writer.Context = modifiedContext;
            }

            return writer;
        }
    }
}
