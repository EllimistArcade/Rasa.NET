using System;

namespace Rasa.Cryptography
{
    using Auth;

    /// <summary>
    /// The auth stream: 16-round Blowfish with a fixed key over a body laid out as the payload
    /// padded to 8 bytes with 0xCC, then an 8 byte trailer - the XOR of every dword before it,
    /// then one dword that is not part of the check. The client's frames have the same shape:
    /// a 10 byte server-list request arrives as a 24 byte body, a 37 byte login as 48.
    /// </summary>
    public static class AuthCryptManager
    {
        /// <summary>The checksum dword and the dword after it.</summary>
        private const int TrailerLength = 8;

        /// <summary>
        /// Decrypts the body in place and reports whether it checks out. A body that is not a
        /// whole number of cipher blocks, or has no room for the trailer, or whose checksum does
        /// not match, is not a frame from the client - the caller drops the connection.
        /// </summary>
        public static bool Decrypt(byte[] data, int offset, int length, ClientCryptData cryptData = null)
        {
            if (length < TrailerLength || length % 8 != 0)
                return false;

            Blowfish.Decrypt(data, offset, length);

            return VerifyChecksum(data, offset, length);
        }

        public static void Encrypt(byte[] data, int offset, ref int length, int maxLength, ClientCryptData cryptData = null)
        {
            var oldLen = length;

            // Make the length a multiple of 8
            var rem = length % 8;
            if (rem != 0)
                length += 8 - rem;

            if (length + TrailerLength > maxLength)
                throw new ArgumentOutOfRangeException(nameof(length), "The length can't exceed the maximal buffer length!");

            // Fill up extra padding bytes
            for (var i = oldLen; i < length; ++i)
                data[offset + i] = 0xCC;

            // Add checksum bytes to the length
            length += TrailerLength;

            AppendChecksum(data, offset, length);

            Blowfish.Encrypt(data, offset, length);
        }

        /// <summary>
        /// True when the checksum dword at the start of the trailer is the XOR of every dword
        /// before it - which is to say, when XORing everything up to and including it gives 0.
        /// </summary>
        private static bool VerifyChecksum(byte[] data, int offset, int length)
        {
            var chksum = 0U;

            // Both bounds are positions in data. The loop used to run from the position to the
            // length, which only lines up when the body sits in the first pool block; in every
            // other block the position is already past the length, the loop runs zero times
            // and everything passes. The result was never looked at anyway, so nothing noticed.
            var end = offset + length - (TrailerLength - 4);

            for (var i = offset; i < end; i += 4)
                chksum ^= BitConverter.ToUInt32(data, i);

            return chksum == 0;
        }

        private static void AppendChecksum(byte[] data, int offset, int length)
        {
            var chksum = 0U;
            var dataEnd = length - TrailerLength;

            for (var i = 0; i < dataEnd; i += 4)
                chksum ^= BitConverter.ToUInt32(data, offset + i);

            Array.Copy(BitConverter.GetBytes(chksum), 0, data, offset + dataEnd, 4);
        }
    }
}
