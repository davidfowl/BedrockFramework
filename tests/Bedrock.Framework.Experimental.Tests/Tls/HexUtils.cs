using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bedrock.Framework.Experimental.Tests.Tls
{
    internal static class HexUtils
    {
        public static byte[] HexToByteArray(this string hex)
        {
            hex = string.Join("", hex.Where(c => !char.IsWhiteSpace(c) && c != '-'));
            var NumberChars = hex.Length;
            var bytes = new byte[NumberChars / 2];
            for (var i = 0; i < NumberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
