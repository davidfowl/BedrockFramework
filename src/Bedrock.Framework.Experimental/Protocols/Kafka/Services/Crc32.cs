using System;
using System.Collections.Generic;
using System.Linq;

// Taken from https://rosettacode.org/wiki/CRC-32#C.23 on 1/19/2020
// Most likely temporary.
namespace Bedrock.Framework.Experimental.Protocols.Kafka.Services
{
    /// <summary>
    /// Performs 32-bit reversed cyclic redundancy checks.
    /// </summary>
    public static class Crc32
    {
        /// <summary>
        /// Generator polynomial (modulo 2) for the reversed CRC32 algorithm. 
        /// </summary>
        private const uint generator = 0xEDB88320;

        /// <summary>
        /// Contains a cache of calculated checksum chunks.
        /// </summary>
        // Constructs the checksum lookup table. Used to optimize the checksum.
        private readonly static uint[] checksumTable = Enumerable
            .Range(0, 256)
            .Select(i =>
            {
                var tableEntry = (uint)i;
                for (var j = 0; j < 8; ++j)
                {
                    tableEntry = ((tableEntry & 1) != 0)
                        ? (generator ^ (tableEntry >> 1))
                        : (tableEntry >> 1);
                }

                return tableEntry;
            })
            .ToArray();

        /// <summary>
        /// Calculates the checksum of the byte stream.
        /// </summary>
        /// <param name="byteStream">The byte stream to calculate the checksum for.</param>
        /// <returns>A 32-bit reversed checksum.</returns>
        public static uint Get<T>(IEnumerable<T> byteStream)
        {
            try
            {
                // Initialize checksumRegister to 0xFFFFFFFF and calculate the checksum.
                return ~byteStream.Aggregate(
                    seed: 0xFFFFFFFF,
                    func: (checksumRegister, currentByte) =>
                          (checksumTable[(checksumRegister & 0xFF)
                          ^ Convert.ToByte(currentByte)]
                          ^ (checksumRegister >> 8)));
            }
            catch (FormatException e)
            {
                throw new InvalidOperationException("Could not read the stream out as bytes.", e);
            }
            catch (InvalidCastException e)
            {
                throw new InvalidOperationException("Could not read the stream out as bytes.", e);
            }
            catch (OverflowException e)
            {
                throw new InvalidOperationException("Could not read the stream out as bytes.", e);
            }
        }
    }
}
