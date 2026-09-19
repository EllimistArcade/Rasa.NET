using System;
using System.IO;
using System.Text;

namespace Rasa.Extensions
{
    public static class BinaryWriterExtensions
    {
        public static void WriteLengthedString(this BinaryWriter writer, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                writer.Write(0);
                return;
            }

            // The reader takes this count to ReadBytes, so it has to be the encoded length.
            // string.Length is UTF-16 code units, which is short for anything outside ASCII -
            // this carries account names, emails and passwords between Auth and Game, and one
            // accented character used to desync that link for the rest of the packet.
            writer.Write(Encoding.UTF8.GetByteCount(text));
            writer.WriteUtf8StringOn(text);
        }

        public static void WriteUtf8String(this BinaryWriter writer, string text)
        {
            writer.WriteUtf8StringOn(text);
        }

        /// <summary>
        /// Writes the text as UTF-8. A <paramref name="length"/> is a fixed field width in bytes,
        /// zero padded; -1 means "however long it turns out to be".
        /// </summary>
        public static void WriteUtf8StringOn(this BinaryWriter writer, string text, int length = -1)
        {
            var bytes = Encoding.UTF8.GetBytes(text);

            if (length == -1)
                length = bytes.Length;

            // Padding used to be counted in characters against a field measured in bytes, so a
            // non-ASCII value both overran the field and was padded too far.
            if (bytes.Length > length)
                throw new ArgumentException(
                    $"'{text}' is {bytes.Length} bytes and does not fit a {length} byte field.", nameof(text));

            writer.Write(bytes);

            for (var i = bytes.Length; i < length; ++i)
                writer.Write((byte) 0);
        }

        public static void WriteAt(this BinaryWriter writer, byte value, long position)
        {
            var currentPosition = writer.BaseStream.Position;

            writer.BaseStream.Position = position;

            writer.Write(value);

            writer.BaseStream.Position = currentPosition;
        }

        public static void WriteAt(this BinaryWriter writer, sbyte value, long position)
        {
            var currentPosition = writer.BaseStream.Position;

            writer.BaseStream.Position = position;

            writer.Write(value);

            writer.BaseStream.Position = currentPosition;
        }

        public static void WriteAt(this BinaryWriter writer, short value, long position)
        {
            var currentPosition = writer.BaseStream.Position;

            writer.BaseStream.Position = position;

            writer.Write(value);

            writer.BaseStream.Position = currentPosition;
        }

        public static void WriteAt(this BinaryWriter writer, ushort value, long position)
        {
            var currentPosition = writer.BaseStream.Position;

            writer.BaseStream.Position = position;

            writer.Write(value);

            writer.BaseStream.Position = currentPosition;
        }

        public static void WriteAt(this BinaryWriter writer, int value, long position)
        {
            var currentPosition = writer.BaseStream.Position;

            writer.BaseStream.Position = position;

            writer.Write(value);

            writer.BaseStream.Position = currentPosition;
        }

        public static void WriteAt(this BinaryWriter writer, uint value, long position)
        {
            var currentPosition = writer.BaseStream.Position;

            writer.BaseStream.Position = position;

            writer.Write(value);

            writer.BaseStream.Position = currentPosition;
        }

        public static void WriteAt(this BinaryWriter writer, long value, long position)
        {
            var currentPosition = writer.BaseStream.Position;

            writer.BaseStream.Position = position;

            writer.Write(value);

            writer.BaseStream.Position = currentPosition;
        }

        public static void WriteAt(this BinaryWriter writer, ulong value, long position)
        {
            var currentPosition = writer.BaseStream.Position;

            writer.BaseStream.Position = position;

            writer.Write(value);

            writer.BaseStream.Position = currentPosition;
        }

        public static void WriteAt(this BinaryWriter writer, float value, long position)
        {
            var currentPosition = writer.BaseStream.Position;

            writer.BaseStream.Position = position;

            writer.Write(value);

            writer.BaseStream.Position = currentPosition;
        }

        public static void WriteAt(this BinaryWriter writer, double value, long position)
        {
            var currentPosition = writer.BaseStream.Position;

            writer.BaseStream.Position = position;

            writer.Write(value);

            writer.BaseStream.Position = currentPosition;
        }
    }
}
