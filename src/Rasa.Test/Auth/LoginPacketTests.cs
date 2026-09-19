using System;
using System.IO;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Rasa.Test.Auth
{
    using Rasa.Packets.Auth.Client;

    /// <summary>
    /// Known answers for the login packet's decryption, so the cipher underneath it can be
    /// replaced and shown to give the same bytes.
    ///
    /// The client sends 30 bytes - name at 0..13, password at 14..29 - and encrypts only the
    /// first 24 of them with single DES, ECB, key "TEST\0\0\0\0". The last six go as they are,
    /// so a password over ten characters is partly in the clear. That is the client's doing and
    /// the server has to match it, so it is pinned here along with everything else.
    ///
    /// The ciphertexts were produced by pycryptodome, not by BouncyCastle, so they check the
    /// server's DES against an independent one rather than against itself.
    /// </summary>
    [TestClass]
    public class LoginPacketTests
    {
        // "Ellimist" / "hunter2", game 1, CD key 0x1234. Everything that is sent sits inside
        // the encrypted 24 bytes.
        private static readonly byte[] VectorA =
        {
            0xAC, 0x2D, 0x76, 0xEB, 0x53, 0x16, 0x90, 0x73, 0x3E, 0x9A, 0xC8, 0x28, 0x90, 0x9B, 0xB9, 0xA8,
            0xF4, 0x07, 0xEF, 0xEF, 0xA2, 0x5A, 0xA2, 0xBE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00,
            0x34, 0x12
        };

        // "abcdefghijklmn" / "0123456789ABCDEF", game 0xDEADBEEF, CD key 0xFFFF. Both fields are
        // full length, so neither has a terminator, and the password runs past byte 24: its
        // last six characters, "ABCDEF", are on the wire as they are.
        private static readonly byte[] VectorB =
        {
            0x19, 0x58, 0x20, 0xA9, 0xEA, 0xFE, 0x7F, 0xBB, 0xB2, 0x84, 0x42, 0x8A, 0x31, 0xCE, 0xF3, 0x5D,
            0x33, 0x89, 0x1D, 0xE6, 0x7E, 0x26, 0xD8, 0xD8, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46,
            0xEF, 0xBE, 0xAD, 0xDE,
            0xFF, 0xFF
        };

        private static LoginPacket Read(byte[] wire)
        {
            var packet = new LoginPacket();

            using var reader = new BinaryReader(new MemoryStream(wire));
            packet.Read(reader);

            return packet;
        }

        [TestMethod]
        public void DecryptsShortNameAndPassword()
        {
            var packet = Read(VectorA);

            Assert.AreEqual("Ellimist", packet.UserName);
            Assert.AreEqual("hunter2", packet.Password);
            Assert.AreEqual(1u, packet.GameId);
            Assert.AreEqual((ushort)0x1234, packet.CDKey);
        }

        [TestMethod]
        public void DecryptsFullLengthFieldsWithNoTerminator()
        {
            var packet = Read(VectorB);

            Assert.AreEqual("abcdefghijklmn", packet.UserName);
            Assert.AreEqual("0123456789ABCDEF", packet.Password);
            Assert.AreEqual(0xDEADBEEFu, packet.GameId);
            Assert.AreEqual((ushort)0xFFFF, packet.CDKey);
        }

        [TestMethod]
        public void LeavesTheLastSixBytesAsTheyAre()
        {
            // Change only the clear tail. If those bytes went through the cipher they would come
            // out as noise; they come out exactly as written.
            var wire = (byte[])VectorB.Clone();
            Array.Copy(Encoding.ASCII.GetBytes("UVWXYZ"), 0, wire, 24, 6);

            Assert.AreEqual("0123456789UVWXYZ", Read(wire).Password);
        }

        [TestMethod]
        public void ConsumesExactlyThirtySixBytes()
        {
            var packet = new LoginPacket();
            var stream = new MemoryStream(VectorA);

            using var reader = new BinaryReader(stream);
            packet.Read(reader);

            Assert.AreEqual(36L, stream.Position);
        }

        [TestMethod]
        public void GivesTheSameAnswerEveryTime()
        {
            // The decrypter is one static instance shared by every login. Whatever replaces it
            // must hold no state between calls.
            var first = Read(VectorA);
            Read(VectorB);
            var again = Read(VectorA);

            Assert.AreEqual(first.UserName, again.UserName);
            Assert.AreEqual(first.Password, again.Password);
        }
    }
}
