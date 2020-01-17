using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.LibCrypto
{
    internal static partial class LibCrypto
    {
        [DllImport(Libraries.LibCrypto, CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void* HMAC(EVP_HashType evp_md, void* key, int key_len, void* d, int n, void* md, ref int md_len);

        public unsafe static int HMAC(EVP_HashType evp, ReadOnlySpan<byte> key, ReadOnlySpan<byte> data, Span<byte> output)
        {
            fixed (void* keyPtr = &MemoryMarshal.GetReference(key))
            fixed (void* dataPtr = &MemoryMarshal.GetReference(data))
            fixed (void* outputPtr = &MemoryMarshal.GetReference(output))
            {
                var outputLength = output.Length;
                var result = HMAC(evp, keyPtr, key.Length, dataPtr, data.Length, outputPtr, ref outputLength);
                ThrowOnNullPointer(result);
                return outputLength;
            }
        }
    }
}
