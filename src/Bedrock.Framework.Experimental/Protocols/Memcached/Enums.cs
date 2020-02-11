using System;
using System.Collections.Generic;
using System.Text;

namespace Bedrock.Framework.Experimental.Protocols.Memcached
{
    public class Enums
    {
        /// <summary>
        /// see https://github.com/memcached/memcached/wiki/BinaryProtocolRevamped#data-types
        /// </summary>
        public enum Opcode : byte
        {
            Get = 0x00,
            Set = 0x01
        }

        public enum ResponseStatus : byte
        {
            NoError = 0x0000,
            KeyNotFound = 0x0001,
            KeyExists = 0x0002,
            ValueTooLarge = 0x0003,
            InvalidArguments = 0x0004,
            ItemNotStored = 0x0005,
            IncrOrDecrInvalid = 0x0006,
            VBucketBelongToAnotherServer = 0x0007,
            AuthenticationError = 0x0008,
            AuthenticationContinue = 0x0009,
            UnknowCommand = 0x0081,
            OutOfMemory = 0x0082,
            NotSupported = 0x0083,
            InternalError = 0x0084,
            Busy = 0x0085,
            TemporaryFailure = 0x0086
        }
    }
}
