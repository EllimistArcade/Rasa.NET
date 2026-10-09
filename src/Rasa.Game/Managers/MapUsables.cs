using System.Collections.Generic;
using System.Linq;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.MapChannel.Server;

    /// <summary>
    /// Usables the client's own map files place, given the state the client draws them in.
    ///
    /// The client's map loader makes every entity of a .map itself, under the id the map gives it
    /// (client/gamemap.py _LoadEntities3: CreateEntity(classId, entityId), then AddToWorld), so a
    /// usable among them is a usable on the client with no state: Usable.__init__ leaves
    /// _curStateId None, and only Recv_UsableInfo, Recv_ForceState and Recv_Use from the server
    /// set one. Until one is set it plays no state animation and carries no state effect
    /// (UsableState.__call__: SwapMesh, AttachStateSFX, PlayAnimation). The map's entities are in
    /// the client's entity manager (AddEntity) under those ids, as the server's own are, and a
    /// method call is sent to an id; ForceState to one sets its state.
    ///
    /// Each is sent its state as the player arrives on the map (AssignPlayer), on every arrival,
    /// as the client makes the map's entities anew on every load. A copy of a map loads the same
    /// file and gets the same.
    ///
    /// The Forean fire pits. UsableTwoStateForeanFirePitV01 (6137) and V03 (6212) are two-state
    /// switches (augmentation 9) whose fire is the special effect of USE_TS_STATE_0 (55)
    /// (usabledata.specialFX (class, 55, USE_STATE_NULL): arch_forean_fire_pit_v01.pkg and
    /// _v03.pkg, the flames and their heat shimmer), with the state's animation
    /// (usabledata.animation: Usable state 0 static, arch_forean_fire_pit.anm on both meshes).
    /// USE_TS_STATE_1 (56) has neither: the pit cold. The .maps attach no particles of their own
    /// to them, so with no state they have stood unlit. There are three, one on each of three
    /// maps; V02 (6211) is on none, and has no effect of its own. Entity ids, positions and
    /// facings are the .maps'.
    /// </summary>
    public static class MapUsables
    {
        public sealed class Usable
        {
            public Usable(uint mapContextId, ulong entityId, uint classId, UseObjectState state, string name)
            {
                MapContextId = mapContextId;
                EntityId = entityId;
                ClassId = classId;
                State = state;
                Name = name;
            }

            public uint MapContextId { get; }

            /// <summary>The id the .map gives it, which is the client's id for it.</summary>
            public ulong EntityId { get; }

            public uint ClassId { get; }
            public UseObjectState State { get; }
            public string Name { get; }

            public override string ToString() => $"{Name} ({EntityId}, class {ClassId}, map {MapContextId})";
        }

        public const uint ConcordiaDivide = 1148;
        public const uint ConcordiaPalisades = 1244;
        public const uint ValverdePlateau = 1497;

        public const uint ForeanFirePitV01 = 6137;     // UsableTwoStateForeanFirePitV01
        public const uint ForeanFirePitV03 = 6212;     // UsableTwoStateForeanFirePitV03

        public static readonly IReadOnlyList<Usable> All = new[]
        {
            // Thoria Das, beside the weapon vendor (202.9, 166.7, 925.6).
            new Usable(ConcordiaDivide, 132770324522631, ForeanFirePitV03, UseObjectState.TsState0, "Thoria Das fire pit"),

            // Mount Reverence, by its waypoint (-765.7, 442.9, 424.2).
            new Usable(ValverdePlateau, 134269267723598, ForeanFirePitV03, UseObjectState.TsState0, "Mount Reverence fire pit"),

            // Below the Temple of the Raging Patriarch (-575.0, 187.6, -110.0).
            new Usable(ConcordiaPalisades, 133182640922310, ForeanFirePitV01, UseObjectState.TsState0, "Raging Patriarch fire pit"),
        };

        /// <summary>The map's usables that are given a state.</summary>
        public static IEnumerable<Usable> OnMap(uint mapContextId) => All.Where(usable => usable.MapContextId == mapContextId);

        /// <summary>
        /// Gives the player's client the state of each usable of the map they have arrived on.
        /// Returns how many were sent.
        /// </summary>
        public static int PlayerEnteredMap(Client client)
        {
            var map = client?.Player?.MapChannel?.MapInfo;

            if (map == null)
                return 0;

            var sent = 0;

            foreach (var usable in OnMap(map.MapContextId))
            {
                client.CallMethod(usable.EntityId, new ForceStatePacket(usable.State, 0));
                sent++;
            }

            return sent;
        }
    }
}
