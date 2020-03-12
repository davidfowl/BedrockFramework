using Bedrock.Framework.Infrastructure;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Amqp
{
    public class ProtocolHelper
    {
        public static Dictionary<string, object> ReadTable(ref SequenceReader<byte> reader)
        {
            var start = reader.CurrentSpanIndex;
            Dictionary<string, object> result = new Dictionary<string, object>();
            reader.TryReadBigEndian(out int tableLength);

            while (reader.CurrentSpanIndex <= (tableLength + start))
            {
                reader.TryRead(out var strLenght);
                string key = Encoding.ASCII.GetString(reader.Sequence.Slice(reader.CurrentSpanIndex, strLenght).FirstSpan);

                reader.Advance(strLenght);
                reader.TryRead(out var type);
                object value = null;
                switch ((char)type)
                {
                    case 't':
                        reader.TryRead(out var r);
                        value = r != 0;
                        break;
                    case 'b':
                        throw new NotImplementedException("type b");
                    case 'B':
                        throw new NotImplementedException("type B");
                    case 'U':
                        throw new NotImplementedException("type U");
                    case 'u':
                        throw new NotImplementedException("type B");
                    case 'i':
                        throw new NotImplementedException("type i");
                    case 'I':
                        throw new NotImplementedException("type I");
                    case 'l':
                        throw new NotImplementedException("type l");
                    case 'L':
                        throw new NotImplementedException("type L");
                    case 'f':
                        throw new NotImplementedException("type f");
                    case 'd':
                        throw new NotImplementedException("type d");
                    case 'D':
                        throw new NotImplementedException("type D");
                    case 's':
                        throw new NotImplementedException("type s");
                    case 'S':
                        value = ReadLongString(ref reader);
                        break;
                    case 'A':
                        throw new NotImplementedException("type A");
                    case 'T':
                        throw new NotImplementedException("type T");
                    case 'F':
                        value = ReadTable(ref reader);
                        break;
                    default: throw new Exception("Unknow field type");
                }
                result.TryAdd(key, value);
            }
            return result;
        }

        public static ReadOnlyMemory<byte> ReadLongString(ref SequenceReader<byte> reader)
        {
            reader.TryReadBigEndian(out int length);
            var result = reader.Sequence.Slice(reader.CurrentSpanIndex, length).ToMemory();
            reader.Advance(length);
            return result;
        }

        public static ReadOnlyMemory<byte> ReadShortString(ref SequenceReader<byte> reader)
        {
            reader.TryRead(out byte length);
            var result = reader.Sequence.Slice(reader.CurrentSpanIndex, length).ToMemory();
            reader.Advance(length);
            return result;
        }

        public static byte BoolArrayToByte(bool[] source)
        {
            if (source.Length > 8)
            {
                return 0;
            } 

            byte result = 0;
            int index = 8 - source.Length;
            foreach (bool b in source)
            {
                if (b)
                    result |= (byte)(1 << (7 - index));
                index++;
            }
            return result;
        }

    }
}
