using System;
using System.IO;

namespace Rasa.Memory
{
    /// <summary>
    /// How many bytes a piece of Python-serialised data takes on the wire. Every outgoing
    /// packet is written into one fixed pool block (8192 bytes on the game port), so a reply
    /// that lists things - search results, a petition with its body - has to be sized before
    /// it is sent, or the write throws inside Send and the client asking is disconnected.
    /// </summary>
    public static class PythonSize
    {
        /// <summary>
        /// What one CallMethod payload may take. The block is 8192 bytes; the length header,
        /// the call-method envelope and the cipher padding come off that, and this leaves a
        /// margin on top.
        /// </summary>
        public const int PayloadBudget = 7168;

        /// <summary>
        /// A list's count is one byte up to twelve entries and two or three beyond, so the
        /// envelope measured around an empty list grows by up to two bytes as rows are added.
        /// Budget loops leave this much room for it.
        /// </summary>
        public const int ListHeaderSlack = 4;

        public static int Of(Action<PythonWriter> write)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            using var pw = new PythonWriter(writer);

            write(pw);

            return (int)stream.Length;
        }

        public static int Of(IPythonDataStruct data)
        {
            return Of(data.Write);
        }
    }
}
