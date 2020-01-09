#nullable enable

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public static class SequenceReaderExtensions
    {
        public static string? ReadNullableString(this ref SequenceReader<byte> reader)
        {
            if (!reader.TryReadBigEndian(out short length))
            {
                // TODO: do something
            }

            if (length == -1)
            {
                return null;
            }

            reader.Rewind(sizeof(short));

            var value = ReadString(ref reader);

            return value;
        }

        public static string ReadString(this ref SequenceReader<byte> reader)
        {
            if (!reader.TryReadBigEndian(out short length)
                || length < 0)
            {
                // TODO: do something
            }

            if (length == 0)
            {
                return "";
            }

            // This should exist: https://github.com/dotnet/corefx/issues/26104
            // if(Utf8Parser.TryParse(reader.CurrentSpan.Slice(0, length), out string value))
            var value = Encoding.UTF8.GetString(reader.UnreadSpan.Slice(0, length));

            reader.Advance(length);

            return value;
        }

        public static KafkaErrorCode ReadErrorCode(this ref SequenceReader<byte> reader)
        {
            if (!reader.TryReadBigEndian(out short errorCode))
            {
                // TODO: do something
            }

            return (KafkaErrorCode)errorCode;
        }

        public static short ReadInt16BigEndian(this ref SequenceReader<byte> reader)
        {
            if (!reader.TryReadBigEndian(out short value))
            {
                // TODO: do something
            }

            return value;
        }
        public static bool ReadBool(this ref SequenceReader<byte> reader)
        {
            if (!reader.TryRead(out byte value))
            {
                // TODO: do something
            }

            // anything other than 0 is true
            return value == 0
                ? false
                : true;
        }

        public static int ReadInt32BigEndian(this ref SequenceReader<byte> reader)
        {
            if (!reader.TryReadBigEndian(out int value))
            {
                // TODO: do something
            }

            return value;
        }
    }
}

#nullable restore