using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Structures;

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
    ///
    /// Lighting and putting out. The client offers a usable it knows Use on click: the right-click
    /// menu's USE_OBJECT (Usable.OnGetUseAction) while it is enabled, which it is from the start,
    /// and not locked, which one with no state is (IsLocked: _curStateId == the lock state, None
    /// == None). Use is RequestUseObject (USE_OBJECT 80 with the class's use arg, 1 for these:
    /// usabledata.lookup has none), and the server's Use (Recv_Use) moves it to the state it
    /// names. A two-state switch's transitions are 55 to 56 and 56 to 55
    /// (usableaugmentationstatetransition 9), neither with an animation or effect of its own: out,
    /// the state's fire is taken off (UsableStateTransition: DetachStateSFX, then 56's, which is
    /// none); lit, 55's is put back with its animation. The request is answered as a footlocker's
    /// is: the windup to the user, the recovery to whoever is near, and at the recovery the pit
    /// goes to its other state on that map channel and every client on it is sent the Use.
    /// One use of a pit at a time; one interrupted changes nothing.
    ///
    /// A pit stays as it was left for as long as its channel runs; whoever arrives is sent it as
    /// it is. A new channel, and so a restart, starts it lit.
    /// </summary>
    public static class MapUsables
    {
        public sealed class Usable
        {
            public Usable(uint mapContextId, ulong entityId, uint classId, Vector3 position, UseObjectState state, UseObjectState? usedState, string name)
            {
                MapContextId = mapContextId;
                EntityId = entityId;
                ClassId = classId;
                Position = position;
                State = state;
                UsedState = usedState;
                Name = name;
            }

            public uint MapContextId { get; }

            /// <summary>The id the .map gives it, which is the client's id for it.</summary>
            public ulong EntityId { get; }

            public uint ClassId { get; }

            /// <summary>Where the .map puts it.</summary>
            public Vector3 Position { get; }

            /// <summary>The state a map channel starts it in.</summary>
            public UseObjectState State { get; }

            /// <summary>The state a use takes it to from <see cref="State"/>, and back; null if it is not to be used.</summary>
            public UseObjectState? UsedState { get; }

            public string Name { get; }

            /// <summary>The state a use takes it to from <paramref name="current"/>, or null.</summary>
            public UseObjectState? UsedFrom(UseObjectState current) =>
                UsedState == null ? null : current == State ? UsedState : current == UsedState ? State : null;

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
            new Usable(ConcordiaDivide, 132770324522631, ForeanFirePitV03, new Vector3(202.922f, 166.713f, 925.613f),
                UseObjectState.TsState0, UseObjectState.TsState1, "Thoria Das fire pit"),

            // Mount Reverence, by its waypoint (-765.7, 442.9, 424.2).
            new Usable(ValverdePlateau, 134269267723598, ForeanFirePitV03, new Vector3(-765.687f, 442.858f, 424.154f),
                UseObjectState.TsState0, UseObjectState.TsState1, "Mount Reverence fire pit"),

            // Below the Temple of the Raging Patriarch (-575.0, 187.6, -110.0).
            new Usable(ConcordiaPalisades, 133182640922310, ForeanFirePitV01, new Vector3(-575.0f, 187.6259f, -110.0f),
                UseObjectState.TsState0, UseObjectState.TsState1, "Raging Patriarch fire pit"),
        };

        /// <summary>How long a use takes, as a footlocker's (DynamicObjectManager, Lockbox).</summary>
        public const int UseWindupMs = 100;

        /// <summary>Each map channel's states, of the usables used on it.</summary>
        private static readonly ConditionalWeakTable<MapChannel, Dictionary<ulong, UseObjectState>> States = new();

        /// <summary>The map's usables that are given a state.</summary>
        public static IEnumerable<Usable> OnMap(uint mapContextId) => All.Where(usable => usable.MapContextId == mapContextId);

        /// <summary>The usable with this id, on any map, or null.</summary>
        public static Usable Find(ulong entityId) => entityId == 0 ? null : All.FirstOrDefault(usable => usable.EntityId == entityId);

        /// <summary>The state a usable is in on a map channel.</summary>
        public static UseObjectState StateOf(MapChannel mapChannel, Usable usable)
        {
            if (mapChannel != null && States.TryGetValue(mapChannel, out var states) && states.TryGetValue(usable.EntityId, out var state))
                return state;

            return usable.State;
        }

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
                client.CallMethod(usable.EntityId, new ForceStatePacket(StateOf(client.Player.MapChannel, usable), 0));
                sent++;
            }

            return sent;
        }

        /// <summary>
        /// RequestUseObject: whether it names one of these. If it does it is answered here, used
        /// or refused, and is nothing else's to answer.
        /// </summary>
        public static bool TryRequestUse(Client client, RequestUseObjectPacket packet)
        {
            var usable = Find(packet?.EntityId ?? 0);

            if (usable == null)
                return false;

            var player = client?.Player;
            var mapChannel = player?.MapChannel;

            if (player == null || mapChannel?.MapInfo == null || client.State != ClientState.Ingame)
                return true;

            // Another map's: the client has no such entity, so only a client that made it up asks.
            if (mapChannel.MapInfo.MapContextId != usable.MapContextId)
            {
                Logger.WriteLog(LogType.Security, $"{player.FamilyName} asked to use {usable}, which is not on their map. Ignored.");
                return true;
            }

            if (packet.ActionId != ActionId.UseObject || usable.UsedState == null)
            {
                ActorManager.RefuseRequest(client, packet.ActionId, packet.ActionArgId, PlayerMessage.PmUseObjectNotUsable);
                return true;
            }

            if (player.State == CharacterState.Dead)
            {
                ActorManager.RefuseRequest(client, packet.ActionId, packet.ActionArgId, PlayerMessage.PmCannotPerformActionNow);
                return true;
            }

            if (Vector3.Distance(player.Position, usable.Position) > DynamicObjectManager.MaxUseDistance)
            {
                ActorManager.RefuseRequest(client, packet.ActionId, packet.ActionArgId, PlayerMessage.PmTargetOutOfRange);
                return true;
            }

            // One use of anything at a time for the player, and one use of the pit at a time.
            if (mapChannel.PerformRecovery.Any(action => action.ActionId == ActionId.UseObject &&
                    (action.Actor == player || action.SourceId == usable.EntityId)))
            {
                ActorManager.RefuseRequest(client, packet.ActionId, packet.ActionArgId, PlayerMessage.PmCannotPerformActionNow);
                return true;
            }

            client.CallMethod(player.EntityId, new PerformWindupPacket(PerformType.TwoArgs, packet.ActionId, packet.ActionArgId));
            mapChannel.PerformRecovery.Add(new ActionData(player, packet.ActionId, packet.ActionArgId, UseWindupMs) { SourceId = usable.EntityId });

            return true;
        }

        /// <summary>Whether a use-object action is a use of one of these.</summary>
        public static bool IsUseOf(ActionData action) =>
            action != null && action.ActionId == ActionId.UseObject && Find(action.SourceId) != null;

        /// <summary>
        /// The recovery of a use of one of these: unless it was interrupted, or its user died or
        /// left the map in the meantime, the usable goes to its other state on the channel, and
        /// every client on the channel is told. Returns the state it went to, or null.
        /// </summary>
        public static UseObjectState? UseRecovery(MapChannel mapChannel, ActionData action)
        {
            var usable = Find(action?.SourceId ?? 0);

            if (usable == null || mapChannel?.MapInfo?.MapContextId != usable.MapContextId || action.IsInrerrupted)
                return null;

            if (action.Actor is not Manifestation user || user.State == CharacterState.Dead || user.MapChannel != mapChannel)
                return null;

            var next = usable.UsedFrom(StateOf(mapChannel, usable));

            if (next == null)
                return null;

            States.GetOrCreateValue(mapChannel)[usable.EntityId] = next.Value;

            // Everyone on the map has it, near or not: the client loads the whole .map. One
            // still loading it is sent the state when it arrives (PlayerEnteredMap).
            foreach (var client in mapChannel.ClientList.ToList())
                if (client?.Player != null && !client.AwaitingMapLoaded)
                    client.CallMethod(usable.EntityId, new UsePacket(user.EntityId, next.Value, 0));

            return next;
        }
    }
}
