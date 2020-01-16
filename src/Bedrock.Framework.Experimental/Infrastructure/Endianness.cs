using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Infrastructure
{
    public static class Endianness
    {
        public static unsafe T FromNetworkOrder<T>(T value) where T : unmanaged
        {
            if (BitConverter.IsLittleEndian) return Reverse(value);
            return value;
        }

        public static unsafe T ToNetworkOrder<T>(T value) where T : unmanaged
        {
            if (BitConverter.IsLittleEndian) return Reverse(value);
            return value;
        }

        public static unsafe T Reverse<T>(T value) where T : unmanaged
        {
            var len = sizeof(T);
            if (len == 1)
            {
                return value;
            }
            else if (len == 2)
            {
                var val = Unsafe.Read<ushort>(&value);
                val = (ushort)((val >> 8) | (val << 8));
                return Unsafe.Read<T>(&val);
            }
            else if (len == 4)
            {
                var val = Unsafe.Read<uint>(&value);
                val = (val << 24)
                    | ((val & 0xFF00) << 8)
                    | ((val & 0xFF0000) >> 8)
                    | (val >> 24);
                return Unsafe.Read<T>(&val);
            }
            else if (len == 8)
            {
                var val = Unsafe.Read<ulong>(&value);
                val = (val << 56)
                    | ((val & 0xFF00) << 40)
                    | ((val & 0xFF0000) << 24)
                    | ((val & 0xFF000000) << 8)
                    | ((val & 0xFF00000000) >> 8)
                    | ((val & 0xFF0000000000) >> 24)
                    | ((val & 0xFF000000000000) >> 40)
                    | (val >> 56);
                return Unsafe.Read<T>(&val);
            }
            else if (len < 512)
            {
                var val = stackalloc byte[len];
                Unsafe.Write(val, value);
                int to = len >> 1, dest = len - 1;
                for (var i = 0; i < to; i++)
                {
                    var tmp = val[i];
                    val[i] = val[dest];
                    val[dest--] = tmp;
                }
                return Unsafe.Read<T>(val);
            }
            else
            {
                var val = new byte[len];
                fixed (void* valPointer = val)
                {
                    Unsafe.Write(valPointer, value);
                    int to = len >> 1, dest = len - 1;
                    for (var i = 0; i < to; i++)
                    {
                        var tmp = val[i];
                        val[i] = val[dest];
                        val[dest--] = tmp;
                    }

                    return Unsafe.Read<T>(valPointer);
                }
            }
        }

    }
}
