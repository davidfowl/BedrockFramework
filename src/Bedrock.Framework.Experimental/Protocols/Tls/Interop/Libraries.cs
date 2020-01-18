using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop
{
    internal static partial class Libraries
    {
        internal const string LibCrypto = "libcrypto-1_1-x64";
        internal const string LibSsl = "libssl-1_1-x64";
        internal const string BCrypt = "BCrypt.dll";

        //For Linux
        //internal const string LibCrypto = "libcrypto.so.1.1";
        //internal const string LibSsl = "libssl.so.1.1";
    }
}
