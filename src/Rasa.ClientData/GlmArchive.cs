using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Rasa.ClientData
{
    /// <summary>
    /// A Tabula Rasa <c>.glm</c> archive (data/mesh01.glm, data/maps/&lt;map&gt;/t&lt;id&gt;_terrain.glm, ...).
    /// The file is a sequence of 1024-byte-aligned chunks, a name table, and a <c>CHNKBLXX</c>
    /// directory whose offset is the last dword of the file. Directory: magic(8), nameTableOffset,
    /// nameTableSize, count, then <c>count</c> 22-byte entries: offset, compressedSize, size, nameOffset,
    /// version (u16), timestamp. A chunk whose compressed size equals its size is stored raw;
    /// otherwise it is a zlib stream.
    /// </summary>
    public sealed class GlmArchive : IDisposable
    {
        public sealed class Entry
        {
            public string Name;
            public long Offset;
            public int CompressedSize;
            public int Size;
            public ushort Version;
            public uint Timestamp;
        }

        private readonly FileStream _stream;
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public string Path { get; }
        public IReadOnlyDictionary<string, Entry> Entries => _entries;

        public GlmArchive(string path)
        {
            Path = path;
            _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            ReadDirectory();
        }

        /// <summary>Reads only the directory: cheap, so all ten mesh archives can be indexed at startup.</summary>
        private void ReadDirectory()
        {
            var tail = new byte[4];
            _stream.Seek(-4, SeekOrigin.End);
            ReadExactly(tail, 4);
            var dirOffset = BitConverter.ToUInt32(tail, 0);

            var header = new byte[20];
            _stream.Seek(dirOffset, SeekOrigin.Begin);
            ReadExactly(header, 20);

            if (Encoding.ASCII.GetString(header, 0, 8) != "CHNKBLXX")
                throw new InvalidDataException($"{Path}: no CHNKBLXX directory at {dirOffset}");

            var nameTableOffset = BitConverter.ToUInt32(header, 8);
            var nameTableSize = BitConverter.ToInt32(header, 12);
            var count = BitConverter.ToInt32(header, 16);

            var names = new byte[nameTableSize];
            _stream.Seek(nameTableOffset, SeekOrigin.Begin);
            ReadExactly(names, nameTableSize);

            // The trailer (last entry may be cut short by the final offset dword in some files).
            var entriesLength = (int)Math.Min((long)count * 22, _stream.Length - 4 - (dirOffset + 20));
            var raw = new byte[entriesLength];
            _stream.Seek(dirOffset + 20, SeekOrigin.Begin);
            ReadExactly(raw, entriesLength);

            for (var i = 0; i + 22 <= entriesLength; i += 22)
            {
                var entry = new Entry
                {
                    Offset = BitConverter.ToUInt32(raw, i),
                    CompressedSize = BitConverter.ToInt32(raw, i + 4),
                    Size = BitConverter.ToInt32(raw, i + 8),
                    Version = BitConverter.ToUInt16(raw, i + 16),
                    Timestamp = BitConverter.ToUInt32(raw, i + 18)
                };

                var nameOffset = BitConverter.ToInt32(raw, i + 12);

                if (nameOffset < 0 || nameOffset >= nameTableSize)
                    break;

                var end = Array.IndexOf(names, (byte)0, nameOffset);

                if (end < 0)
                    end = nameTableSize;

                entry.Name = Encoding.ASCII.GetString(names, nameOffset, end - nameOffset);
                _entries[entry.Name] = entry;
            }
        }

        public bool Contains(string name) => _entries.ContainsKey(name);

        /// <summary>The chunk's bytes, inflated when it is compressed.</summary>
        public byte[] Read(string name)
        {
            if (!_entries.TryGetValue(name, out var entry))
                throw new FileNotFoundException($"{name} is not in {Path}");

            return Read(entry);
        }

        public byte[] Read(Entry entry)
        {
            var compressed = new byte[entry.CompressedSize];

            lock (_stream)
            {
                _stream.Seek(entry.Offset, SeekOrigin.Begin);
                ReadExactly(compressed, entry.CompressedSize);
            }

            if (entry.CompressedSize == entry.Size)
                return compressed;

            // zlib: 2-byte header, deflate body, adler32 trailer (which DeflateStream ignores).
            var output = new byte[entry.Size];
            using var input = new MemoryStream(compressed, 2, compressed.Length - 2);
            using var inflate = new DeflateStream(input, CompressionMode.Decompress);
            var total = 0;

            while (total < output.Length)
            {
                var read = inflate.Read(output, total, output.Length - total);

                if (read <= 0)
                    break;

                total += read;
            }

            if (total != output.Length)
                throw new InvalidDataException($"{entry.Name} in {Path}: inflated {total} of {entry.Size} bytes");

            return output;
        }

        private void ReadExactly(byte[] buffer, int count)
        {
            var total = 0;

            while (total < count)
            {
                var read = _stream.Read(buffer, total, count - total);

                if (read <= 0)
                    throw new EndOfStreamException(Path);

                total += read;
            }
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
