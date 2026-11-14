using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Models;
    using Packets.MapChannel.Server;
    using Packets.Protocol;
    using Structures;
    using D = Services.Preloader.DevilsDenContent;

    /// <summary>
    /// Places where the client's collision holds a player the navmesh lets through: a corner
    /// they can walk into and not out of. The server cannot change what the client collides
    /// with, so a living player who stays in one for <see cref="HoldMs"/> is put down at its way
    /// out, on open floor a few metres off. Every copy of the map has them.
    ///
    /// Each is a box around the corner and where to put them; the way out is put on the navmesh
    /// under it. A box is in world coordinates, or in the frame of a structure the server puts
    /// down (<see cref="Spot.Origin"/>, <see cref="Spot.Yaw"/>), and then counts only while the
    /// structure stands in that copy (<see cref="Spot.Standing"/>): the navmesh is built from the
    /// .map, so what the server puts down is open floor to it - to a walk, a knockback's landing
    /// and /stuck's clear ground alike.
    /// </summary>
    public static class StuckSpots
    {
        /// <summary>How long a player has to stay in one to be taken out.</summary>
        public const long HoldMs = 4_000;

        /// <summary>
        /// A corner: its map, what it is, its box, and the way out (world coordinates). The box is
        /// in its own frame: the world's, or a structure's when <see cref="Origin"/> and
        /// <see cref="Yaw"/> say where that stands.
        /// </summary>
        public sealed record Spot(uint Map, string Name, Vector3 Min, Vector3 Max, Vector3 Out)
        {
            /// <summary>Where the box's frame is, and its turn about the vertical, as a placed object's (CreateFromYawPitchRoll).</summary>
            public Vector3 Origin { get; init; }
            public float Yaw { get; init; }

            /// <summary>What of the box counts, in its frame; null for all of it.</summary>
            public Func<Vector3, bool> Within { get; init; }

            /// <summary>Whether it is there in a copy of the map; null for always.</summary>
            public Func<MapChannel, bool> Standing { get; init; }

            /// <summary>A position in the box's frame.</summary>
            public Vector3 Local(Vector3 position) => Yaw == 0f
                ? position - Origin
                : Vector3.Transform(position - Origin, Quaternion.CreateFromYawPitchRoll(-Yaw, 0f, 0f));

            /// <summary>A position in the box's frame, in the world.</summary>
            public Vector3 World(Vector3 local) => InFrame(Origin, Yaw, local);

            public bool Holds(Vector3 position)
            {
                var local = Local(position);

                return local.X >= Min.X && local.X <= Max.X && local.Y >= Min.Y && local.Y <= Max.Y &&
                    local.Z >= Min.Z && local.Z <= Max.Z && (Within == null || Within(local));
            }
        }

        public static readonly Spot[] Spots = new[]
        {
            // The cave under Pinhole Falls (Wilderness): a sandbag pile pushed into the rock, which
            // leans out over the corner between them at head height. Out onto the cave floor to
            // the south east.
            new Spot(1220, "Pinhole Falls cave, where the sandbags meet the rock",
                new Vector3(335.4f, 228.0f, 475.4f), new Vector3(337.0f, 230.4f, 477.2f), new Vector3(338.5f, 228.9f, 474.0f))
        }.Concat(DevilsDenGenerator()).ToArray();

        /// <summary>
        /// The Devil's Den generator up the path (UsableInertDestBaneOutpostGenerator,
        /// arch_bane_outpost_generator_v01, the server's placement DevilsDenContent.GeneratorRow): a
        /// hollow hall 17 by 16 m with no floor, its roof half a cylinder along its length - 7.83 m
        /// round an axis 0.13 m under its base, 7.7 m high in the middle and 1 m at the sides - and
        /// a narrow tank out from each end, 27 m end to end. It is put down on a slope: the ground
        /// under it runs from 1.8 m under its base to 3.4 m over it, its downhill end wall and side
        /// stand up to 1.3 m clear of the ground and the tank at that end 1.9 m, and on the uphill
        /// side the ground runs over the roof's edge. A player who gets in under the downhill
        /// edge, or is put inside by anything that goes by the navmesh, stands on the slope inside
        /// with the walls round them (a report at -123, 134.2, 165.5). Its wreck's collision is
        /// the same.
        ///
        /// Under the hall's roof, half a metre in from it, or in a tank below 3 m - not on the
        /// roof. Out 2.9 m off the hall's downhill side (local 11.5, -7), on open floor joined to
        /// the floor inside.
        /// </summary>
        private static IEnumerable<Spot> DevilsDenGenerator()
        {
            const string name = "Devil's Den generator up the path";
            const float radius = 7.83f - 0.5f, axis = -0.13f;
            var at = D.GeneratorAt;
            var yaw = (float)D.GeneratorYaw;
            var outside = InFrame(at, yaw, new Vector3(11.5f, 0f, -7f));
            static bool standing(MapChannel map) => PlacedDestructibles.LiveOf(map, D.GeneratorRow) != null;

            yield return new Spot(D.Den, $"{name}: under its hall's roof",
                new Vector3(-8.4f, -3.5f, -7.0f), new Vector3(8.4f, 7.0f, 7.0f), outside)
            {
                Origin = at, Yaw = yaw, Standing = standing,
                Within = local => local.X * local.X + (local.Y - axis) * (local.Y - axis) < radius * radius
            };

            yield return new Spot(D.Den, $"{name}: in a tank at its end",
                new Vector3(-2.0f, -3.5f, -13.0f), new Vector3(2.0f, 3.0f, 13.0f), outside)
            {
                Origin = at, Yaw = yaw, Standing = standing,
                Within = local => Math.Abs(local.Z) > 7.0f
            };
        }

        /// <summary>Who is in which spot of a copy of the map, and since when.</summary>
        private static readonly ConditionalWeakTable<MapChannel, Dictionary<uint, (Spot Spot, long Since)>> States = new();

        /// <summary>The clock; a test's to replace.</summary>
        public static Func<long> Now { get; set; } = () => Environment.TickCount64;

        /// <summary>A position in the frame of something standing at origin, turned by yaw, in the world.</summary>
        public static Vector3 InFrame(Vector3 origin, float yaw, Vector3 local) =>
            (yaw == 0f ? local : Vector3.Transform(local, Quaternion.CreateFromYawPitchRoll(yaw, 0f, 0f))) + origin;

        /// <summary>The spot a position is in on a map, or null, whether or not what it is round stands.</summary>
        public static Spot At(uint map, Vector3 position) => Spots.FirstOrDefault(spot => spot.Map == map && spot.Holds(position));

        /// <summary>The spot a position is in in a copy of a map, or null: only those that are there in it.</summary>
        public static Spot At(MapChannel map, Vector3 position)
        {
            var mapId = map?.MapInfo?.MapContextId ?? 0;

            return Spots.FirstOrDefault(spot => spot.Map == mapId && (spot.Standing == null || spot.Standing(map)) && spot.Holds(position));
        }

        /// <summary>From the map channel worker.</summary>
        public static void Worker(MapChannel map)
        {
            var mapId = map?.MapInfo?.MapContextId ?? 0;

            if (mapId == 0 || !Spots.Any(spot => spot.Map == mapId))
                return;

            var held = States.GetValue(map, _ => new Dictionary<uint, (Spot, long)>());
            var now = Now();
            var present = new HashSet<uint>();

            foreach (var client in map.ClientList?.ToList() ?? new List<Client>())
            {
                var player = client?.Player;

                if (player == null || client.State != ClientState.Ingame || player.MapChannel != map || player.State == CharacterState.Dead)
                    continue;

                var spot = At(map, player.Position);

                if (spot == null)
                    continue;

                present.Add(player.Id);

                if (!held.TryGetValue(player.Id, out var entry) || entry.Spot != spot)
                {
                    held[player.Id] = (spot, now);
                    continue;
                }

                if (now - entry.Since < HoldMs)
                    continue;

                held.Remove(player.Id);
                TakeOut(map, client, spot);
            }

            foreach (var id in held.Keys.Where(id => !present.Contains(id)).ToList())
                held.Remove(id);
        }

        private static void TakeOut(MapChannel map, Client client, Spot spot)
        {
            var player = client.Player;
            var from = player.Position;
            var to = NavMeshManager.SnapToGround(map, spot.Out);

            player.PlaceAt(to);
            client.CellMoveObject(client, new MoveObjectMessage(player.EntityId,
                new Movement(to, client.Movement?.ViewDirection ?? new Vector2(0f, 0f))), false);

            Logger.WriteLog(LogType.Debug, $"Character {player.Id} taken out of a stuck spot ({spot.Name}) at {from}, to {to}.");
        }

        /// <summary>Forgets every copy; for tests.</summary>
        public static void Reset() => States.Clear();
    }
}
