using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Rasa.ClientData
{
    /// <summary>
    /// Every <c>.geo</c> in the client's <c>data/mesh*.glm</c> archives, by file name, with the parsed
    /// collision cached. Names are not alphabetical across archives (mesh05 holds the terrain pieces,
    /// mesh06 props and water, the rest architecture and avatars), so all directories are read up front.
    /// </summary>
    public sealed class MeshLibrary : IDisposable
    {
        private readonly List<GlmArchive> _archives = new List<GlmArchive>();
        private readonly Dictionary<string, (GlmArchive archive, GlmArchive.Entry entry)> _index = new Dictionary<string, (GlmArchive, GlmArchive.Entry)>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GeoMesh> _cache = new Dictionary<string, GeoMesh>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The class id to mesh file table (entity_meshes.csv: class_id, class_name, mesh).</summary>
        private readonly Dictionary<int, string> _classMesh = new Dictionary<int, string>();

        public int MeshCount => _index.Count;
        public int ClassCount => _classMesh.Count;

        public MeshLibrary(string clientDataDirectory, string entityMeshCsv)
        {
            foreach (var path in Directory.EnumerateFiles(clientDataDirectory, "mesh*.glm").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var archive = new GlmArchive(path);
                _archives.Add(archive);

                foreach (var entry in archive.Entries.Values)
                    _index[entry.Name] = (archive, entry);
            }

            foreach (var line in File.ReadLines(entityMeshCsv).Skip(1))
            {
                var parts = line.Split(',');

                if (parts.Length < 3 || !int.TryParse(parts[0], out var classId))
                    continue;

                _classMesh[classId] = parts[parts.Length - 1].Trim();
            }
        }

        public bool TryGetMeshName(int classId, out string mesh) => _classMesh.TryGetValue(classId, out mesh);

        /// <summary>The parsed mesh, or null when no archive has it. Parsed once, then cached.</summary>
        public GeoMesh Get(string meshName)
        {
            if (meshName == null)
                return null;

            lock (_cache)
            {
                if (_cache.TryGetValue(meshName, out var cached))
                    return cached;
            }

            GeoMesh mesh = null;

            if (_index.TryGetValue(meshName, out var location))
            {
                try
                {
                    mesh = GeoMesh.Parse(location.archive.Read(location.entry));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"  {meshName}: {e.Message}");
                }
            }

            lock (_cache)
                _cache[meshName] = mesh;

            return mesh;
        }

        public GeoMesh GetForClass(int classId)
        {
            return _classMesh.TryGetValue(classId, out var name) ? Get(name) : null;
        }

        public void Dispose()
        {
            foreach (var archive in _archives)
                archive.Dispose();
        }
    }
}
