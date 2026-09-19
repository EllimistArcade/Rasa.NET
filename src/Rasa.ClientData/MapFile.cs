using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Rasa.ClientData
{
    /// <summary>
    /// The static entities of a client <c>data/maps/&lt;name&gt;/&lt;name&gt;.map</c> (format 2.43 - 2.45,
    /// the versions the 1.16.5.0 client ships; see client/gamemap.py <c>_GameMapLoader2</c>).
    /// Only what geometry needs is kept: class id, world position, orientation, scale and the local
    /// bounds the map stores per entity. The orientation quaternion is stored w, x, y, z.
    /// </summary>
    public sealed class MapFile
    {
        public sealed class Entity
        {
            public int ClassId;
            public ulong EntityId;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
            public int Flags;
            public Vector3 BoundsMin;
            public Vector3 BoundsMax;
        }

        public int MinorVersion { get; private set; }
        public int TemplateVersion { get; private set; }
        public readonly List<Entity> Entities = new List<Entity>();

        public static MapFile Load(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            var map = new MapFile();
            map.Read(reader);
            return map;
        }

        private void Read(BinaryReader r)
        {
            var format = r.ReadInt32();
            var major = (format >> 16) & 0xFFFF;
            MinorVersion = format & 0xFFFF;
            TemplateVersion = r.ReadInt32();

            if (major != 2 || MinorVersion < 43 || MinorVersion > 45)
                throw new InvalidDataException($"map format {major}.{MinorVersion} is not supported (2.43 - 2.45)");

            r.ReadInt32(); // map status

            var terrainCount = r.ReadInt32();
            for (var i = 0; i < terrainCount; i++)
                r.ReadInt32();

            r.ReadBytes(16); // terrain vis settings: 2 ints, 2 floats

            if (MinorVersion >= 45)
            {
                var count = r.ReadInt32();

                for (var i = 0; i < count; i++)
                {
                    var e = new Entity { ClassId = r.ReadInt32() };
                    var lo = r.ReadUInt32();
                    var hi = r.ReadUInt32();
                    e.EntityId = ((ulong)hi << 32) | lo;
                    e.Position = ReadVector3(r);
                    var w = r.ReadSingle(); var x = r.ReadSingle(); var y = r.ReadSingle(); var z = r.ReadSingle();
                    e.Rotation = new Quaternion(x, y, z, w);
                    r.ReadBytes(8); // two hue colours
                    e.Scale = r.ReadSingle();
                    e.Flags = r.ReadInt32();
                    r.ReadInt32(); // cull layer
                    var particles = r.ReadInt32();

                    for (var p = 0; p < particles; p++)
                        r.ReadBytes(21); // int, int, 3 floats, byte

                    e.BoundsMin = ReadVector3(r);
                    e.BoundsMax = ReadVector3(r);
                    Entities.Add(e);
                }
            }
            else
            {
                var entitySize = r.ReadInt32();
                var count = r.ReadInt32();

                for (var i = 0; i < count; i++)
                {
                    var record = r.ReadBytes(entitySize);
                    var e = new Entity
                    {
                        ClassId = BitConverter.ToInt32(record, 0),
                        EntityId = ((ulong)BitConverter.ToUInt32(record, 8) << 32) | BitConverter.ToUInt32(record, 4),
                        Position = new Vector3(BitConverter.ToSingle(record, 12), BitConverter.ToSingle(record, 16), BitConverter.ToSingle(record, 20)),
                        Rotation = new Quaternion(BitConverter.ToSingle(record, 28), BitConverter.ToSingle(record, 32), BitConverter.ToSingle(record, 36), BitConverter.ToSingle(record, 24)),
                        Scale = BitConverter.ToSingle(record, 44),
                        Flags = BitConverter.ToInt32(record, 48)
                    };
                    var particles = BitConverter.ToInt32(record, 56);

                    for (var p = 0; p < particles; p++)
                        r.ReadBytes(21);

                    var isWater = r.ReadInt32();

                    if (isWater != 0)
                    {
                        var waterVersion = r.ReadInt32();
                        r.ReadBytes(waterVersion == 1 ? 172 : waterVersion == 2 ? 44 : 0);
                    }

                    Entities.Add(e);
                }
            }
        }

        private static Vector3 ReadVector3(BinaryReader r)
        {
            return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }
    }
}
