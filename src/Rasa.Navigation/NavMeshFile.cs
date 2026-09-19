using System.IO;
using DotRecast.Detour;
using DotRecast.Detour.Io;

namespace Rasa.Navigation
{
    /// <summary>
    /// One navmesh per map, as <c>&lt;directory&gt;/&lt;map name&gt;.nav</c>: a Detour tiled mesh set in
    /// DotRecast's <c>MSET</c> format (the recast4j variant, which records the vertices-per-polygon
    /// limit the C format leaves implicit), written by Rasa.NavMesh and read here. Map names are the
    /// client folder names in lower case.
    /// </summary>
    public static class NavMeshFile
    {
        public const string Extension = ".nav";

        public static string PathFor(string directory, string mapName)
        {
            return Path.Combine(directory, mapName.ToLowerInvariant() + Extension);
        }

        public static void Write(string path, DtNavMesh navMesh)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            new DtMeshSetWriter().Write(writer, navMesh, DotRecast.Core.RcByteOrder.LITTLE_ENDIAN, false);
        }

        public static DtNavMesh Read(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            return new DtMeshSetReader().Read(reader);
        }
    }
}
