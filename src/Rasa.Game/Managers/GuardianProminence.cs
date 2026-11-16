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

    using G = Services.Preloader.GuardianProminenceContent;

    /// <summary>
    /// What Guardian Prominence (1416) does that its rows do not, in each copy of it - a squad's
    /// instance, or the map's own channel - for the missions GuardianProminenceContent authors, as
    /// the wiki and the walkthrough tell it:
    ///
    ///  - The five Biomechanical Jamming Towers (PlacedDestructibles): each destroyed is its objective
    ///    of No Soap Radio for everyone on it, and stays down in the copy.
    ///  - The Chamber of Eternity's door (ForceFields, Kind.Door), shut: a member on The Chamber Of
    ///    Eternity, who carries the Guardian Stone, or one who has done it, within
    ///    <see cref="DoorReach"/> of it opens it for the copy, for good.
    ///  - "Now stand back, I'm about to unleash the dogs of war!": the first member to take Breaking
    ///    The Chain Of Command brings two Juggernauts out of the open field at the camp, which march
    ///    on it and fight whoever they meet. The camp's soldiers beat them if left alone. Once a copy.
    ///  - No Soap Radio turned in by a member: Cronauer's reinforcements, four soldiers and a mech,
    ///    come to the camp and stay. Once a copy.
    ///  - The instance's people take no harm.
    /// Kept by the copy and not saved.
    /// </summary>
    public static class GuardianProminence
    {
        public const float DoorReach = 8f;

        internal sealed class State
        {
            public bool Set;
            public readonly PlacedDestructibles.Live[] Towers = new PlacedDestructibles.Live[G.Towers.Length];
            public ForceFields.Field Door;
            public bool AssaultStarted;
            public readonly List<Creature> Juggernauts = new();
            public bool ReinforcementsCalled;
            public readonly List<Creature> Reinforcements = new();

            /// <summary>The events already recorded: the assignment, the mission, the scenario, the event.</summary>
            public readonly HashSet<(string Assignment, uint Mission, uint Scenario, uint Event)> Told = new();
        }

        private static readonly ConditionalWeakTable<MapChannel, State> States = new();

        public static bool IsProminence(MapChannel map) => map?.MapInfo?.MapContextId == G.Prominence;

        internal static State StateOf(MapChannel map) => IsProminence(map) ? States.GetValue(map, _ => new State()) : null;

        public static IReadOnlyList<PlacedDestructibles.Live> Towers(MapChannel map) => StateOf(map)?.Towers ?? Array.Empty<PlacedDestructibles.Live>();

        public static ForceFields.Field Door(MapChannel map) => StateOf(map)?.Door;

        /// <summary>Whether the Chamber of Eternity's door is open in the copy.</summary>
        public static bool DoorOpen(MapChannel map) => StateOf(map)?.Door is { IsDestroyed: true };

        public static bool AssaultStarted(MapChannel map) => StateOf(map)?.AssaultStarted ?? false;

        /// <summary>The copy's Juggernauts, alive.</summary>
        public static List<Creature> Juggernauts(MapChannel map) => StateOf(map)?.Juggernauts.Where(Alive).ToList() ?? new List<Creature>();

        /// <summary>The copy's reinforcements, alive.</summary>
        public static List<Creature> Reinforcements(MapChannel map) => StateOf(map)?.Reinforcements.Where(Alive).ToList() ?? new List<Creature>();

        /// <summary>From the map channel worker.</summary>
        public static void Worker(MapChannel map)
        {
            var state = StateOf(map);

            if (state == null)
                return;

            if (!state.Set)
                SetUp(map, state);

            ProtectAfs(map);
            KeepDown(state);

            var members = Members(map);

            if (members.Count == 0)
                return;

            OpenDoor(map, state, members);
            Assault(map, state, members);
            Reinforce(map, state, members);

            foreach (var client in members)
                Tell(state, client);
        }

        #region Set up

        private static void SetUp(MapChannel map, State state)
        {
            state.Set = true;

            for (var i = 0; i < G.Towers.Length; i++)
            {
                var tower = G.Towers[i];
                var live = PlacedDestructibles.PutDown(map, (EntityClasses)G.TowerClass, NavMeshManager.SnapToGround(map, tower.At), tower.Yaw,
                    G.TowerHitPoints);

                if (live?.Object != null)
                    live.Object.NameOverrideId = G.TowerName;

                state.Towers[i] = live;
            }

            var door = ForceFields.Place(map, ForceFields.ClassOf("elohdoor"), ForceFields.Side.B, G.DoorAt, G.DoorYaw, 1);
            door.Sealed = true;
            door.Object.NameOverrideId = G.DoorName;
            state.Door = door;

            Logger.WriteLog(LogType.Debug, $"Guardian Prominence {map.InstanceId}: {state.Towers.Count(live => live != null)} jamming towers " +
                "and the Chamber of Eternity's door put down.");
        }

        /// <summary>What is destroyed stays so: never due back.</summary>
        private static void KeepDown(State state)
        {
            foreach (var live in state.Towers)
                if (live != null && live.IsDown && live.RespawnAt != long.MaxValue)
                    live.RespawnAt = long.MaxValue;
        }

        #endregion

        #region The Chamber of Eternity

        /// <summary>The shut door with a member who carries the Guardian Stone at it: open, for good.</summary>
        private static void OpenDoor(MapChannel map, State state, List<Client> members)
        {
            if (state.Door is not { IsDestroyed: false } door)
                return;

            var by = members.FirstOrDefault(client => HoldsTheStone(client.Player) &&
                Vector3.Distance(client.Player.Position, G.DoorAt) <= DoorReach);

            if (by == null)
                return;

            ForceFields.Damage(door, door.Health, by.Player.EntityId);
            Logger.WriteLog(LogType.Debug, $"Guardian Prominence {map.InstanceId}: {by.Player.FamilyName} opens the Chamber of Eternity with the Guardian Stone.");
        }

        /// <summary>On The Chamber Of Eternity, which Professor Anand gives the stone with, or done with it.</summary>
        public static bool HoldsTheStone(Manifestation player) =>
            player.Missions.TryGetValue(G.ChamberOfEternity, out var log) && log.State == MissionState.Active || Done(player, G.ChamberOfEternity);

        #endregion

        #region The Juggernauts

        private static void Assault(MapChannel map, State state, List<Client> members)
        {
            if (state.AssaultStarted)
                return;

            var by = members.FirstOrDefault(client =>
                client.Player.Missions.TryGetValue(G.ChainOfCommand, out var log) && log.State == MissionState.Active);

            if (by == null)
                return;

            state.AssaultStarted = true;

            foreach (var from in G.JuggernautsFrom)
                if (Spawn(map, G.Juggernaut, from, G.Camp, immune: false) is { } juggernaut)
                {
                    state.Juggernauts.Add(juggernaut);
                    var facing = Math.Atan2(G.Camp.X - juggernaut.Position.X, G.Camp.Z - juggernaut.Position.Z);

                    if (BehaviorManager.Instance.SetActionScriptedMove(map, juggernaut, NavMeshManager.SnapToGround(map, G.Camp), facing) &&
                        juggernaut.Controller.ScriptedMove != null)
                        juggernaut.Controller.ScriptedMove.FightsBack = true;
                }

            foreach (var client in members)
                CommunicatorManager.Instance.SystemMessage(client, "Bane Juggernauts are coming out of the open field at the Temple of the Ages!");

            Logger.WriteLog(LogType.Debug, $"Guardian Prominence {map.InstanceId}: {by.Player.FamilyName} takes Breaking The Chain Of Command; " +
                $"{state.Juggernauts.Count} Juggernauts march on the camp.");
        }

        #endregion

        #region The reinforcements

        private static void Reinforce(MapChannel map, State state, List<Client> members)
        {
            if (state.ReinforcementsCalled)
                return;

            var by = members.FirstOrDefault(client => Done(client.Player, G.NoSoapRadio));

            if (by == null)
                return;

            state.ReinforcementsCalled = true;

            foreach (var at in G.Reinforcements)
                if (Spawn(map, G.Reinforcement, at, at + new Vector3(0, 0, 10f), immune: true) is { } soldier)
                    state.Reinforcements.Add(soldier);

            if (Spawn(map, G.ReinforcementMech, G.MechAt, G.MechAt + new Vector3(0, 0, 10f), immune: true) is { } mech)
                state.Reinforcements.Add(mech);

            Logger.WriteLog(LogType.Debug, $"Guardian Prominence {map.InstanceId}: Cronauer's reinforcements come to the camp " +
                $"({state.Reinforcements.Count}), called in by {by.Player.FamilyName}.");
        }

        #endregion

        #region The missions

        /// <summary>What the copy has done, to the member's missions that wait on it.</summary>
        private static void Tell(State state, Client client)
        {
            for (var i = 0; i < G.Towers.Length; i++)
                if (state.Towers[i] is { IsDown: true } && Wants(client.Player, G.NoSoapRadio, G.Towers[i].Objective))
                    Record(state, client, G.NoSoapRadio, G.Towers[i].Event);
        }

        #endregion

        #region Helpers

        /// <summary>The instance's people take no harm, each one as it comes into the world again.</summary>
        private static void ProtectAfs(MapChannel map)
        {
            foreach (var creature in map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct())
                if (!creature.ImmuneToAllDamage && Array.IndexOf(G.Afs, creature.DbId) >= 0)
                    creature.ImmuneToAllDamage = true;
        }

        private static Creature Spawn(MapChannel map, uint dbId, Vector3 spot, Vector3 facingTo, bool immune)
        {
            var creature = CreatureManager.Instance.CreateCreature(dbId, null);

            if (creature == null)
                return null;

            var at = NavMeshManager.SnapToGround(map, spot);
            var facing = Math.Atan2(facingTo.X - at.X, facingTo.Z - at.Z);
            CreatureManager.Instance.SetLocation(creature, at, facing, map.MapInfo.MapContextId);
            creature.LastYaw = (float)facing;
            creature.ImmuneToAllDamage = immune;
            CellManager.Instance.AddToWorld(map, creature);
            return creature;
        }

        /// <summary>Alive and still in the world as itself.</summary>
        private static bool Alive(Creature creature) =>
            creature != null && creature.State != CharacterState.Dead && creature.State != CharacterState.Dying &&
            EntityManager.Instance.Creatures.TryGetValue(creature.EntityId, out var registered) && ReferenceEquals(registered, creature);

        private static List<Client> Members(MapChannel map) =>
            map.ClientList?.Where(client => client?.Player != null && client.State == ClientState.Ingame &&
                    client.Player.MapChannel == map && client.Player.State != CharacterState.Dead &&
                    client.Player.State != CharacterState.Dying).ToList()
            ?? new List<Client>();

        private static bool Done(Manifestation player, uint missionId) =>
            player.MissionHistory.TryGetValue(missionId, out var state) && state is MissionState.Completed or MissionState.Success ||
            player.Missions.TryGetValue(missionId, out var log) && log.State is MissionState.Completed or MissionState.Success;

        public static bool Wants(Manifestation player, uint missionId, uint objectiveId) =>
            player.Missions.TryGetValue(missionId, out var log) && log.State == MissionState.Active &&
            log.Objectives.TryGetValue(objectiveId, out var objective) && objective.State == MissionObjectiveState.Incomplete;

        private static void Record(State state, Client client, uint missionId, uint eventId)
        {
            if (!client.Player.Missions.TryGetValue(missionId, out var log))
                return;

            var key = (log.AssignmentId ?? string.Empty, missionId, G.Scenario, eventId);

            if (state.Told.Contains(key))
                return;

            if (MissionApplication.Instance.TryEmitScenarioProgressEvent(client, missionId, G.Scenario, eventId))
                state.Told.Add(key);
        }

        public static void Reset() => States.Clear();

        #endregion
    }
}
