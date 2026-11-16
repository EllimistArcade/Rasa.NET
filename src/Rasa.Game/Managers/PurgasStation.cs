using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Missions;
    using Structures;

    using P = Services.Preloader.PurgasStationContent;
    using M = Services.Preloader.Missions.Purgas.PurgasStationMissionsV1;
    using E = Services.Preloader.Missions.Purgas.PurgasStationMissionsV1.Events;

    /// <summary>
    /// What Purgas Station's top floor does, in each copy of it (map 1806, a squad instance), on what
    /// PurgasStationContent puts down, for the missions PurgasStationMissionsV1 authors - as Ten Ton
    /// Hammer's walkthrough tells it:
    ///
    ///  - The holding room. Coming up to its doorway, a player whose A New Plan of Attack is to
    ///    locate the squad has found it. The Force Field Controller on the overlook takes the field
    ///    across the doorway down for good (it cannot be shot down), whoever works it; everyone in
    ///    the copy whose mission is to release the squad has done so.
    ///  - The CommLink. Coming up to it, a player whose mission is to find one has; what it says is
    ///    the mission's (its scene's menu).
    ///  - Comm. Expert Taylor and his crew are on the overlook once anyone in the copy has done A New
    ///    Plan of Attack, or holds Off Grid; Off Grid is offered by radio here to whoever has done the
    ///    first and not taken the second.
    ///  - The Waypoint Activation Terminal, worked by one whose Off Grid awaits it, activates the
    ///    waypoint: the "Level 2: Auxiliary Storage Chamber" waypoint is everyone's in the copy, and
    ///    their Off Grid has it activated. Round the reactor's top tier to its far side is having
    ///    explored the facility further.
    ///  - The five Thrax Deathtribe Soldiers: each killed counts for the copy, and goes to everyone
    ///    in it with Chain Reaction - who is offered it by radio once one is dead.
    ///
    /// What the world has done goes to every player in the copy whose mission is waiting on it, once
    /// a second, so a player who takes a mission up later - or joins the squad - is caught up. Kept by
    /// the copy and not saved: a copy made again has its field up until one who has released the
    /// squad is in (PlacedDestructibles.CatchUp, the mission's TakeDownField), and the Deathtribe
    /// dead by their pools' clocks, which a squad's instance keeps.
    /// </summary>
    public static class PurgasStation
    {
        /// <summary>How near the holding room's doorway the squad is found, across and up.</summary>
        public const float SquadFoundRadius = 14f, SquadFoundHeight = 6f;

        /// <summary>How near the CommLink it is found.</summary>
        public const float CommLinkFoundRadius = 10f, CommLinkFoundHeight = 5f;

        /// <summary>How far up and down the far side counts.</summary>
        public const float FarSideHeight = 8f;

        /// <summary>How often what the world has done goes to the players waiting on it.</summary>
        public const long CatchUpMs = 1000;

        /// <summary>How long before a mission is offered again to someone who did not take it.</summary>
        public const long OfferAgainMs = 60_000;

        public sealed class State
        {
            internal bool Set;
            public bool Released { get; internal set; }
            public bool WaypointActive { get; internal set; }
            public bool CrewCalled { get; internal set; }
            public bool[] DeathtribeDown { get; } = new bool[P.DeathtribeSpots.Length];

            internal readonly HashSet<uint> SeenAlive = new();
            internal readonly HashSet<string> Told = new();
            internal readonly Dictionary<(uint Character, uint Mission), long> OfferedAt = new();
            internal long NextCatchUpAt;
        }

        private static readonly ConditionalWeakTable<MapChannel, State> States = new();

        /// <summary>The clock; a test's to replace.</summary>
        public static Func<long> Now { get; set; } = () => Environment.TickCount64;

        /// <summary>Gives a player a waypoint, as walking over it would; a test's to replace.</summary>
        public static Action<Client, uint> GrantWaypoint { get; set; } = (client, id) =>
            DynamicObjectManager.Instance.CheckPlayerWaypoint(client, new WaypointInfo(id, false, WaypointType.Waypoint));

        public static bool IsStation(MapChannel map) => map?.MapInfo?.MapContextId == P.Map;

        public static State StateOf(MapChannel map) => IsStation(map) ? States.GetValue(map, _ => new State()) : null;

        #region The worker

        /// <summary>From the map channel worker.</summary>
        public static void Worker(MapChannel map)
        {
            var state = StateOf(map);

            if (state == null)
                return;

            var now = Now();

            if (!state.Set)
                SetUp(map, state);

            var members = Members(map);

            Arrivals(state, members);
            WatchWorld(map, state);

            if (now >= state.NextCatchUpAt)
            {
                state.NextCatchUpAt = now + CatchUpMs;
                CatchUp(map, state, members, now);
            }
        }

        private static void SetUp(MapChannel map, State state)
        {
            state.Set = true;

            // A row's sealed field takes nothing from shots; the controller is what drops it.
            if (PlacedDestructibles.LiveOf(map, P.CellField)?.Field is { } field)
                field.Sealed = true;
        }

        /// <summary>Who has come up to the holding room, the CommLink, the far side.</summary>
        private static void Arrivals(State state, List<Client> members)
        {
            foreach (var client in members)
            {
                var player = client.Player;
                var at = player.Position;

                if (Wants(player, M.NewPlanOfAttack, 4) && Near(at, P.CellDoor, SquadFoundRadius, SquadFoundHeight))
                    Emit(client, M.NewPlanOfAttack, E.Squad, E.SquadFound, once: true, state);

                if (Wants(player, M.NewPlanOfAttack, 11) && Near(at, P.CommLinkAt, CommLinkFoundRadius, CommLinkFoundHeight))
                    Emit(client, M.NewPlanOfAttack, E.Squad, E.CommLinkFound, once: true, state);

                if (Wants(player, M.OffGrid, 9) && Near(at, P.FarSideAt, (float)M.FarSideRadius, FarSideHeight))
                    Emit(client, M.OffGrid, E.Facility, E.FarSideReached, once: true, state);
            }
        }

        /// <summary>Whether a point is within <paramref name="radius"/> of a spot across, and <paramref name="height"/> up or down.</summary>
        public static bool Near(Vector3 at, P.Spot spot, float radius, float height)
        {
            var dx = at.X - (float)spot.X;
            var dz = at.Z - (float)spot.Z;

            return dx * dx + dz * dz <= radius * radius && Math.Abs(at.Y - (float)spot.Y) <= height;
        }

        /// <summary>The field, the Deathtribe: what has happened to them.</summary>
        private static void WatchWorld(MapChannel map, State state)
        {
            // A copy made again, its field taken down by a player who released the squad before.
            if (!state.Released && PlacedDestructibles.LiveOf(map, P.CellField) is { IsDown: true })
                Release(map, state, null);

            for (uint n = 1; n <= state.DeathtribeDown.Length; n++)
            {
                if (state.DeathtribeDown[n - 1] || Pool(map, P.DeathtribePool(n)) is not { } pool)
                    continue;

                if (pool.AliveCreatures > 0 || pool.QueuedCreatures > 0)
                    state.SeenAlive.Add(pool.DbId);
                else if (Cleared(state, pool))
                {
                    state.DeathtribeDown[n - 1] = true;
                    Tell(map, $"A Thrax Deathtribe Soldier is dead: {state.DeathtribeDown.Count(down => down)} of {state.DeathtribeDown.Length}.");
                }
            }
        }

        /// <summary>
        /// Whether a pool has been killed: in a squad instance, dead by its clock (which is kept);
        /// otherwise spawned and nothing of it left. One that has no ground to stand on counts, as
        /// Torcastra's bunkers do: a Deathtribe that cannot be put down is not to hold the mission.
        /// </summary>
        private static bool Cleared(State state, SpawnPool pool)
        {
            if (pool.AliveCreatures > 0 || pool.QueuedCreatures > 0 || pool.DropshipQueue > 0)
                return false;

            if (pool.NoWalkableGround)
                return true;

            return pool.ClearedAtUtcMs != 0 || pool.HasSpawned || state.SeenAlive.Contains(pool.DbId);
        }

        private static void CatchUp(MapChannel map, State state, List<Client> members, long now)
        {
            if (!state.CrewCalled && members.Any(client => Done(client.Player, M.NewPlanOfAttack) || client.Player.Missions.ContainsKey(M.OffGrid)))
                CallCrew(map, state);

            var anyDeathtribe = state.DeathtribeDown.Any(down => down);

            foreach (var client in members)
            {
                var player = client.Player;

                if (state.Released && Wants(player, M.NewPlanOfAttack, 9))
                    Emit(client, M.NewPlanOfAttack, E.Squad, E.SquadReleased, once: true, state);

                if (state.WaypointActive)
                {
                    if (!player.GainedWaypoints.Any(waypoint => waypoint.WaypointId == P.StorageChamberWaypoint))
                        GrantWaypoint(client, P.StorageChamberWaypoint);

                    if (Wants(player, M.OffGrid, 10))
                        Emit(client, M.OffGrid, E.Facility, E.WaypointActivated, once: true, state);
                }

                if (Wants(player, M.ChainReaction, 1))
                    for (uint n = 1; n <= state.DeathtribeDown.Length; n++)
                        if (state.DeathtribeDown[n - 1])
                            Emit(client, M.ChainReaction, E.Deathtribe, n, once: true, state);

                if (anyDeathtribe)
                    Offer(client, state, now, M.ChainReaction, M.ChainReactionOfferKey);

                if (Done(player, M.NewPlanOfAttack))
                    Offer(client, state, now, M.OffGrid, M.OffGridOfferKey);
            }
        }

        /// <summary>Taylor, Gates and Hauffman ported in to the overlook.</summary>
        public static void CallCrew(MapChannel map, State state)
        {
            if (state.CrewCalled)
                return;

            state.CrewCalled = true;

            foreach (var id in new[] { P.TaylorPool, P.GatesPool, P.HauffmanPool })
                Wake(map, id);

            Tell(map, "Comm. Expert Taylor's crew has ported in to the control room overlook.");
        }

        private static void Offer(Client client, State state, long now, uint missionId, string key)
        {
            var player = client.Player;

            if (player.Missions.ContainsKey(missionId) || Done(player, missionId) ||
                state.OfferedAt.TryGetValue((player.Id, missionId), out var at) && now - at < OfferAgainMs)
                return;

            state.OfferedAt[(player.Id, missionId)] = now;
            MissionApplication.Instance?.Offers.TryOffer(client, missionId,
                Rasa.Missions.Definitions.MissionOfferSourceIdentity.ServerEvent(key));
        }

        #endregion

        #region The holding room and the waypoint

        /// <summary>The field across the holding room's doorway down for good in this copy.</summary>
        public static void Release(MapChannel map, State state, Manifestation by)
        {
            if (state.Released)
                return;

            state.Released = true;

            if (PlacedDestructibles.LiveOf(map, P.CellField) is { } live && live.Field is { } field)
            {
                field.Sealed = false;

                if (!field.IsDestroyed)
                    ForceFields.Damage(field, field.Health, by?.EntityId ?? 0);

                live.RespawnAt = long.MaxValue;
            }

            SetUsable(map, P.ReleaseSwitch, UseObjectState.TsState1);
            Tell(map, "The holding room's force field is down.");
        }

        /// <summary>The waypoint to Level 2 activated in this copy.</summary>
        public static void ActivateWaypoint(MapChannel map, State state)
        {
            if (state.WaypointActive)
                return;

            state.WaypointActive = true;
            SetUsable(map, P.WaypointTerminal, UseObjectState.TsState1);
            Tell(map, "The waypoint is active: Level 2: Auxiliary Storage Chamber.");
        }

        #endregion

        #region Usables

        /// <summary>
        /// Whether a player may use one of the station's usables now (PlacedUsables.RefusalOf): null
        /// if they may, or why not.
        /// </summary>
        public static PlayerMessage? RefusalOf(Client client, PlacedUsables.Live live)
        {
            var map = live?.Map;
            var state = StateOf(map);
            var player = client?.Player;

            if (state == null || player == null || live.Placement.MapContextId != P.Map)
                return null;

            switch (live.Placement.Id)
            {
                case P.ReleaseSwitch:
                    return state.Released ? PlayerMessage.PmUseObjectNotUsable : null;

                case P.WaypointTerminal:
                    return state.WaypointActive || Wants(player, M.OffGrid, 10) ? null : PlayerMessage.PmCannotPerformActionNow;
            }

            return null;
        }

        /// <summary>A player has used one of the station's usables (PlacedUsables.UseRecovery), to <paramref name="next"/>.</summary>
        public static void Used(Client client, PlacedUsables.Live live, UseObjectState next)
        {
            var map = live?.Map;
            var state = StateOf(map);

            if (state == null || client?.Player == null || live.Placement.MapContextId != P.Map)
                return;

            switch (live.Placement.Id)
            {
                case P.ReleaseSwitch:
                    live.ResetAt = 0;
                    Release(map, state, client.Player);
                    Emit(client, M.NewPlanOfAttack, E.Squad, E.SquadReleased, once: true, state);
                    break;

                case P.WaypointTerminal:
                    live.ResetAt = 0;
                    ActivateWaypoint(map, state);
                    if (!client.Player.GainedWaypoints.Any(waypoint => waypoint.WaypointId == P.StorageChamberWaypoint))
                        GrantWaypoint(client, P.StorageChamberWaypoint);
                    Emit(client, M.OffGrid, E.Facility, E.WaypointActivated, once: true, state);
                    break;
            }
        }

        #endregion

        #region Helpers

        /// <summary>The living players in the copy.</summary>
        private static List<Client> Members(MapChannel map) =>
            map.ClientList?.Where(client => client?.Player != null && client.State == ClientState.Ingame &&
                    client.Player.MapChannel == map && client.Player.State != CharacterState.Dead &&
                    client.Player.State != CharacterState.Dying).ToList()
            ?? new List<Client>();

        private static SpawnPool Pool(MapChannel map, uint id) => map.SpawnPools.FirstOrDefault(pool => pool.DbId == id);

        /// <summary>A scripted pool set going, to stay going.</summary>
        private static void Wake(MapChannel map, uint id)
        {
            if (Pool(map, id) is { } pool)
                pool.Woken = true;
        }

        private static void SetUsable(MapChannel map, uint id, UseObjectState state)
        {
            if (PlacedUsables.LiveOf(map, id) is { } live && live.State != state)
                PlacedUsables.Switch(live, state, 0);
        }

        private static void Tell(MapChannel map, string text)
        {
            foreach (var client in Members(map))
                CommunicatorManager.Instance.SystemMessage(client, text);
        }

        private static bool Done(Manifestation player, uint missionId) =>
            player.MissionHistory.TryGetValue(missionId, out var state) && state is MissionState.Completed or MissionState.Success ||
            player.Missions.TryGetValue(missionId, out var log) && log.State is MissionState.Completed or MissionState.Success;

        /// <summary>Whether the player has the mission with the objective open.</summary>
        public static bool Wants(Manifestation player, uint missionId, uint objectiveId) =>
            player.Missions.TryGetValue(missionId, out var log) && log.State == MissionState.Active &&
            log.Objectives.TryGetValue(objectiveId, out var objective) && objective.State == MissionObjectiveState.Incomplete;

        /// <summary>
        /// A scenario event to the player's mission. With <paramref name="once"/>, not again for the
        /// same assignment once it has gone through.
        /// </summary>
        private static void Emit(Client client, uint missionId, uint scenarioId, uint eventId, bool once, State state)
        {
            var player = client.Player;

            if (!player.Missions.TryGetValue(missionId, out var log))
                return;

            var key = $"{player.Id}:{log.AssignmentId}:{missionId}:{scenarioId}:{eventId}";

            if (once && state.Told.Contains(key))
                return;

            if (MissionApplication.Instance?.TryEmitScenarioProgressEvent(client, missionId, scenarioId, eventId) == true)
                state.Told.Add(key);
        }

        #endregion

        #region A GM's

        /// <summary>
        /// .purgas: what this copy has done, and a GM's hand in it.
        ///   .purgas                     - the field, the crew, the waypoint, the Deathtribe
        ///   .purgas release             - takes the holding room's field down
        ///   .purgas crew                - ports Taylor's crew in
        ///   .purgas waypoint            - activates the waypoint to Level 2
        ///   .purgas deathtribe &lt;n|all&gt;  - a Deathtribe, or all five, counted dead
        /// </summary>
        public static IEnumerable<string> Command(MapChannel map, Manifestation gm, string[] parts)
        {
            var state = StateOf(map);

            if (state == null)
            {
                yield return "Not in Purgas Station.";
                yield break;
            }

            var sub = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;
            var arg = parts.Length > 2 ? parts[2].ToLowerInvariant() : string.Empty;

            switch (sub)
            {
                case "release":
                    Release(map, state, gm);
                    break;

                case "crew":
                    CallCrew(map, state);
                    break;

                case "waypoint":
                    ActivateWaypoint(map, state);
                    break;

                case "deathtribe":
                    for (var i = 0; i < state.DeathtribeDown.Length; i++)
                        if (arg == "all" || arg == (i + 1).ToString())
                            state.DeathtribeDown[i] = true;
                    break;
            }

            yield return $"Purgas Station {map.InstanceId}: the holding room's field {(state.Released ? "down" : "up")}, " +
                $"Taylor's crew {(state.CrewCalled ? "in" : "not yet in")}, the waypoint {(state.WaypointActive ? "active" : "inactive")}.";
            yield return "Deathtribe: " + string.Join(", ", state.DeathtribeDown.Select((down, i) => $"{i + 1} {(down ? "dead" : "alive")}")) + ".";
        }

        #endregion

        /// <summary>Forgets every copy; for tests.</summary>
        public static void Reset() => States.Clear();
    }
}
