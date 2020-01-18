using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Tls.Interop.BCrypt
{
    internal static partial class BCrypt
    {
        private static class BCryptPropertyStrings
        {
            internal const string BCRYPT_CHAINING_MODE = "ChainingMode";
            internal const string BCRYPT_ECC_PARAMETERS = "ECCParameters";
            internal const string BCRYPT_EFFECTIVE_KEY_LENGTH = "EffectiveKeyLength";
            internal const string BCRYPT_HASH_LENGTH = "HashDigestLength";
            internal const string BCRYPT_ECC_CURVE_NAME_LIST = "ECCCurveNameList";
            internal const string BCRYPT_ECC_CURVE_NAME = "ECCCurveName";
            internal const string BCRYPT_OBJECT_LENGTH = "ObjectLength";
            internal const string BCRYPT_AUTH_TAG_LENGTH = "AuthTagLength";
            internal const string BCRYPT_BLOCK_LENGTH = "BlockLength";
            internal const string BCRYPT_KEY_LENGTH = "KeyLength";
            internal const string BCRYPT_HASH_BLOCK_LENGTH = "HashBlockLength";
        }
    }
}
