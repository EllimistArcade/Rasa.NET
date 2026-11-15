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
    using Packets.MapChannel.Server;
    using Structures;

    using B = Services.Preloader.BrannWaterRefineryContent;

    /// <summary>
    /// What the Brann Water Refinery (2093) does that its rows do not, in each copy of it - a squad's
    /// instance, or the map's own channel - for the missions BrannWaterRefineryContent authors, as
    /// the wiki and Ten Ton Hammer's walkthrough tell it:
    ///
    ///  - Field Officer Lagda, at the way in: she follows the nearest member on We Need Backup!'s
    ///    escort who comes within <see cref="PickupRadius"/>, is put beside them when they are more
    ///    than <see cref="WarpDistance"/> away or where she cannot walk to them, and takes no harm.
    ///    Within <see cref="DeliverRadius"/> of Commander Killroy she is at the operations room, for
    ///    everyone on the escort; and she stays there.
    ///  - The five locked doors (ForceFields, Kind.Door): the way to Captain Jenks, Final
    ///    Processing, the Reservoir Tanks room, Namistai's and the skybridge's. Killroy's passkey
    ///    opens them: a member who has done We Need Backup! within
    ///    <see cref="DoorReach"/> of one opens it for the copy, for good.
    ///  - The three Bane Hacking Modules (PlacedDestructibles): each destroyed is its objective of
    ///    Cripple the Defenses for everyone on it, and stays down in the copy.
    ///  - The ten Bane Detonation Devices: anyone on Defusing the Situation who still has one to
    ///    defuse may defuse it (a <see cref="DefuseMs"/> use), at any pace - there is no timer. Each
    ///    defused gives every member who still has that one to defuse its objective and a
    ///    detonator.
    ///  - The skybridge: its pools all dead at once in the copy is it secured, for good.
    ///  - The sleeping pods, four in each living quarters, and the Greenhouse's four command
    ///    chairs (PlacedDestructibles): a quarters' four down, the chairs' four down, is the
    ///    objective for everyone on it; and they stay down.
    ///  - The refinery's AFS take no harm.
    /// What the copy has done goes to every member whose mission is waiting on it. Kept by the copy
    /// and not saved.
    /// </summary>
    public static class BrannWaterRefinery
    {
        public const int DefuseMs = 3_000;
        public const float PickupRadius = 15f, DeliverRadius = 10f, WarpDistance = 40f, DoorReach = 8f;

        internal sealed class State
        {
            public bool Set;
            public Creature Lagda;
            public ulong LagdaLead;
            public bool LagdaDelivered;
            public bool SkybridgeSecured;
            public readonly ForceFields.Field[] Doors = new ForceFields.Field[B.Doors.Length];
            public readonly PlacedDestructibles.Live[] Modules = new PlacedDestructibles.Live[B.Modules.Length];
            public readonly PlacedDestructibles.Live[] Pods = new PlacedDestructibles.Live[B.Pods.Length];
            public readonly PlacedDestructibles.Live[] Chairs = new PlacedDestructibles.Live[B.Chairs.Length];
            public readonly DynamicObject[] Bombs = new DynamicObject[B.Bombs.Length];

            /// <summary>The events already recorded: the assignment, the mission, the scenario, the event.</summary>
            public readonly HashSet<(string Assignment, uint Mission, uint Scenario, uint Event)> Told = new();
        }

        private static readonly ConditionalWeakTable<MapChannel, State> States = new();

        public static bool IsRefinery(MapChannel map) => map?.MapInfo?.MapContextId == B.Refinery;

        internal static State StateOf(MapChannel map) => IsRefinery(map) ? States.GetValue(map, _ => new State()) : null;

        /// <summary>The copy's Lagda, alive; null if she is not.</summary>
        public static Creature LagdaOf(MapChannel map) => StateOf(map)?.Lagda is { } lagda && Alive(lagda) ? lagda : null;

        /// <summary>Whether Lagda has been brought to the operations room in the copy.</summary>
        public static bool LagdaDelivered(MapChannel map) => StateOf(map)?.LagdaDelivered ?? false;

        /// <summary>Whether the skybridge is secured in the copy.</summary>
        public static bool SkybridgeSecured(MapChannel map) => StateOf(map)?.SkybridgeSecured ?? false;

        /// <summary>The copy's locked doors, in BrannWaterRefineryContent.Doors' order; for a test.</summary>
        public static IReadOnlyList<ForceFields.Field> Doors(MapChannel map) => StateOf(map)?.Doors ?? Array.Empty<ForceFields.Field>();

        public static IReadOnlyList<PlacedDestructibles.Live> Modules(MapChannel map) => StateOf(map)?.Modules ?? Array.Empty<PlacedDestructibles.Live>();

        public static IReadOnlyList<PlacedDestructibles.Live> Pods(MapChannel map) => StateOf(map)?.Pods ?? Array.Empty<PlacedDestructibles.Live>();

        public static IReadOnlyList<PlacedDestructibles.Live> Chairs(MapChannel map) => StateOf(map)?.Chairs ?? Array.Empty<PlacedDestructibles.Live>();

        public static IReadOnlyList<DynamicObject> Bombs(MapChannel map) => StateOf(map)?.Bombs ?? Array.Empty<DynamicObject>();

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
            Skybridge(map, state);

            var members = Members(map);

            if (members.Count == 0)
                return;

            OpenDoors(map, state, members);
            Lagda(map, state, members);

            foreach (var client in members)
                Tell(state, client);
        }

        #region Set up

        private static void SetUp(MapChannel map, State state)
        {
            state.Set = true;

            var doorClass = ForceFields.ClassOf("branndoor");

            for (var i = 0; i < B.Doors.Length; i++)
            {
                var door = ForceFields.Place(map, doorClass, ForceFields.Side.B, B.Doors[i].At, B.Doors[i].Yaw, 1);
                door.Sealed = true;
                state.Doors[i] = door;
            }

            for (var i = 0; i < B.Modules.Length; i++)
                state.Modules[i] = PutDown(map, B.ModuleClass, B.Modules[i].At, B.Modules[i].Yaw, B.ModuleHitPoints, B.ModuleName);

            for (var i = 0; i < B.Pods.Length; i++)
                state.Pods[i] = PutDown(map, B.PodClasses[i % B.PodClasses.Length], B.Pods[i].At, B.Pods[i].Yaw, B.PodHitPoints, B.PodName);

            for (var i = 0; i < B.Chairs.Length; i++)
                state.Chairs[i] = PutDown(map, B.ChairClass, B.Chairs[i].At, B.Chairs[i].Yaw, B.ChairHitPoints, 0);

            for (var i = 0; i < B.Bombs.Length; i++)
                state.Bombs[i] = PlaceBomb(map, i);

            Logger.WriteLog(LogType.Debug, $"Brann Water Refinery {map.InstanceId}: {B.Doors.Length} doors, " +
                $"{state.Modules.Count(live => live != null)} hacking modules, {state.Pods.Count(live => live != null)} pods, " +
                $"{state.Chairs.Count(live => live != null)} chairs and {B.Bombs.Length} bombs put down.");
        }

        private static PlacedDestructibles.Live PutDown(MapChannel map, uint classId, Vector3 at, float yaw, uint hitPoints, uint name)
        {
            var live = PlacedDestructibles.PutDown(map, (EntityClasses)classId, NavMeshManager.SnapToGround(map, at), yaw, hitPoints);

            if (live?.Object != null && name != 0)
                live.Object.NameOverrideId = name;

            return live;
        }

        private static DynamicObject PlaceBomb(MapChannel map, int index)
        {
            var key = $"brann:bomb-{index}";
            var existing = map.DynamicObjects.FirstOrDefault(obj => obj.ScenarioKey == key);

            if (existing != null)
                return existing;

            var at = NavMeshManager.SnapToGround(map, B.Bombs[index].At);
            var obj = DynamicObjectManager.Instance.CreateScenarioDynamicObject(map, (EntityClasses)B.BombClass, at, 0, key, true, DefuseMs);
            obj.StateId = UseObjectState.BombStateArmed;
            obj.NameOverrideId = B.BombName;
            map.DynamicObjects.Add(obj);
            CellManager.Instance.AddToWorld(map, obj);
            obj.IsInWorld = true;
            return obj;
        }

        /// <summary>What is destroyed stays so: never due back.</summary>
        private static void KeepDown(State state)
        {
            foreach (var live in state.Modules.Concat(state.Pods).Concat(state.Chairs))
                if (live != null && live.IsDown && live.RespawnAt != long.MaxValue)
                    live.RespawnAt = long.MaxValue;
        }

        #endregion

        #region The doors

        /// <summary>A shut door with a member who has Killroy's passkey at it: open, for good.</summary>
        private static void OpenDoors(MapChannel map, State state, List<Client> members)
        {
            for (var i = 0; i < state.Doors.Length; i++)
            {
                if (state.Doors[i] is not { IsDestroyed: false } door)
                    continue;

                var by = members.FirstOrDefault(client => HasPasskey(client.Player) &&
                    Vector3.Distance(client.Player.Position, B.Doors[i].At) <= DoorReach);

                if (by == null)
                    continue;

                ForceFields.Damage(door, door.Health, by.Player.EntityId);
                Logger.WriteLog(LogType.Debug, $"Brann Water Refinery {map.InstanceId}: {by.Player.FamilyName}'s passkey opens the door to {B.Doors[i].Name}.");
            }
        }

        /// <summary>Killroy's passkey: We Need Backup! done.</summary>
        public static bool HasPasskey(Manifestation player) => Done(player, B.WeNeedBackup);

        #endregion

        #region Lagda

        private static bool Escorting(Manifestation player) => Wants(player, B.WeNeedBackup, B.EscortLagda);

        private static void Lagda(MapChannel map, State state, List<Client> members)
        {
            if (!Alive(state.Lagda))
            {
                state.Lagda = Spawn(map, B.Lagda, state.LagdaDelivered ? B.LagdaAtOperations : B.LagdaAt, B.KillroyAt);
                state.LagdaLead = 0;
            }

            var lagda = state.Lagda;

            if (lagda == null || state.LagdaDelivered)
                return;

            if (Vector3.Distance(lagda.Position, B.KillroyAt) <= DeliverRadius)
            {
                state.LagdaDelivered = true;
                Stand(map, lagda, NavMeshManager.SnapToGround(map, B.LagdaAtOperations), B.KillroyAt);
                Logger.WriteLog(LogType.Debug, $"Brann Water Refinery {map.InstanceId}: Lagda is at the operations room.");
                return;
            }

            var lead = members.FirstOrDefault(client => client.Player.EntityId == state.LagdaLead && Escorting(client.Player));

            if (lead == null)
            {
                lead = members.Where(client => Escorting(client.Player) &&
                        Vector3.Distance(client.Player.Position, lagda.Position) <= PickupRadius)
                    .OrderBy(client => Vector3.Distance(client.Player.Position, lagda.Position)).FirstOrDefault();

                if (lead == null)
                {
                    if (state.LagdaLead != 0)
                        Stand(map, lagda, lagda.Position, lagda.Position + new Vector3((float)Math.Sin(lagda.Rotation), 0, (float)Math.Cos(lagda.Rotation)));

                    state.LagdaLead = 0;
                    return;
                }

                state.LagdaLead = lead.Player.EntityId;
                lagda.Controller.ScriptedMove = null;
                BehaviorManager.Instance.SetActionFollow(lagda, lead.Player.EntityId);
                CreatureManager.PublishEscortStatus(map, lagda, true);
                Logger.WriteLog(LogType.Debug, $"Brann Water Refinery {map.InstanceId}: Lagda follows {lead.Player.FamilyName}.");
            }

            var to = lead.Player.Position;

            if (Vector3.Distance(lagda.Position, to) <= WarpDistance && map.NavMesh?.Connected(lagda.Position, to) != false)
                return;

            // Too far, or where she cannot walk: put beside them.
            var beside = NavMeshManager.RandomPointAround(map, to, 3f) is { } near && Vector3.Distance(near, to) <= 5f ? near : to;
            beside = NavMeshManager.SnapToGround(map, beside);
            var facing = Math.Atan2(to.X - beside.X, to.Z - beside.Z);

            if (BehaviorManager.Instance.RestoreScriptedPose(map, lagda, beside, facing))
            {
                lagda.Controller.ScriptedMove = null;
                BehaviorManager.Instance.SetActionFollow(lagda, lead.Player.EntityId);
            }
        }

        /// <summary>Lagda stands where she is put, facing the other: nobody's to follow.</summary>
        private static void Stand(MapChannel map, Creature lagda, Vector3 at, Vector3 facingTo)
        {
            lagda.Controller.ActionFollow.FollowTargetId = 0;
            CreatureManager.PublishEscortStatus(map, lagda, false);

            if (BehaviorManager.Instance.RestoreScriptedPose(map, lagda, at, Math.Atan2(facingTo.X - at.X, facingTo.Z - at.Z)))
            {
                lagda.Controller.ScriptedMove = null;
                lagda.Controller.CurrentAction = BehaviorManager.BehaviorActionIdle;
            }
        }

        #endregion

        #region The skybridge

        /// <summary>The skybridge's pools, each with its creatures out and all of them dead at once: secured, for good.</summary>
        private static void Skybridge(MapChannel map, State state)
        {
            if (state.SkybridgeSecured)
                return;

            var pools = B.SkybridgePools.Select(id => map.SpawnPools.FirstOrDefault(pool => pool.DbId == id)).ToList();

            if (pools.Any(pool => pool == null || pool.DeadCreatures == 0 || pool.AliveCreatures > 0 || pool.QueuedCreatures > 0 ||
                    pool.DropshipQueue > 0))
                return;

            state.SkybridgeSecured = true;
            Logger.WriteLog(LogType.Debug, $"Brann Water Refinery {map.InstanceId}: the skybridge is secured.");
        }

        #endregion

        #region The bombs

        /// <summary>Whether a finished action is a use of one of the copy's bombs (ActorActionManager).</summary>
        public static bool IsUseOf(MapChannel map, ActionData action) =>
            action != null && StateOf(map) is { } state && state.Bombs.Any(bomb => bomb != null && bomb.EntityId == action.SourceId);

        /// <summary>
        /// A use about to start (DynamicObjectManager.RequestUseObjectPacket): why it is refused, or
        /// null. A bomb is for a player on Defusing the Situation who has not defused it.
        /// </summary>
        public static PlayerMessage? Refusal(Client client, DynamicObject obj)
        {
            var state = StateOf(client?.Player?.MapChannel);
            var index = state == null || obj == null ? -1 : Array.IndexOf(state.Bombs, obj);

            if (index < 0)
                return null;

            return Wants(client.Player, B.DefusingTheSituation, B.Bombs[index].Objective) ? null : PlayerMessage.PmUseObjectNotUsable;
        }

        /// <summary>A finished use of one of the copy's bombs: what it does.</summary>
        public static void UseRecovery(MapChannel map, ActionData action)
        {
            var state = StateOf(map);
            var index = state == null ? -1 : Array.FindIndex(state.Bombs, bomb => bomb != null && bomb.EntityId == action.SourceId);

            if (index < 0)
                return;

            var obj = state.Bombs[index];
            var client = obj.TriggeredByPlayers.FirstOrDefault(c => c.Player == action.Actor);

            if (client == null)
                return;

            obj.TriggeredByPlayers.Remove(client);

            if (action.IsInrerrupted || Refusal(client, obj) != null)
                return;

            if (!MapInstanceScope.Share(map, client.Player, obj) ||
                Vector3.Distance(client.Player.Position, obj.Position) > DynamicObjectManager.MaxUseDistance)
            {
                Logger.WriteLog(LogType.Security, $"{client.Player.FamilyName} was no longer at object {obj.EntityId} when the use finished; nothing done.");
                return;
            }

            Defuse(map, state, index, client);
        }

        /// <summary>A bomb defused: its objective and a detonator for everyone who still has it to defuse, the defuser first.</summary>
        internal static void Defuse(MapChannel map, State state, int index, Client by)
        {
            var bomb = B.Bombs[index];
            var obj = state.Bombs[index];

            foreach (var client in new[] { by }.Concat(Members(map).Where(member => member != by)))
            {
                if (!Wants(client.Player, B.DefusingTheSituation, bomb.Objective))
                    continue;

                Give(client, B.DetonatorTemplate);
                Record(state, client, B.DefusingTheSituation, bomb.Objective);
            }

            if (obj.StateId != UseObjectState.BombStateDisarmed)
            {
                obj.StateId = UseObjectState.BombStateDisarmed;
                CellManager.Instance.CellCallMethod(map, obj, new UsePacket(by.Player.EntityId, UseObjectState.BombStateDisarmed, 0));
            }

            Logger.WriteLog(LogType.Debug, $"Brann Water Refinery {map.InstanceId}: {by.Player.FamilyName} defuses the bomb in {bomb.Where}.");
        }

        private static void Give(Client client, uint template)
        {
            var item = ItemManager.Instance.CreateFromTemplateId(template, 1);

            if (item == null || InventoryManager.Instance.GrantItemToInventory(client, item) == null)
                Logger.WriteLog(LogType.Error, $"Brann Water Refinery: template {template} could not go to {client.Player.FamilyName}.");
        }

        #endregion

        #region The missions

        /// <summary>What the copy has done, to the member's missions that wait on it.</summary>
        private static void Tell(State state, Client client)
        {
            var player = client.Player;

            if (state.LagdaDelivered && Wants(player, B.WeNeedBackup, B.EscortLagda))
                Record(state, client, B.WeNeedBackup, B.LagdaDelivered);

            for (var i = 0; i < B.Modules.Length; i++)
                if (state.Modules[i] is { IsDown: true } && Wants(player, B.CrippleTheDefenses, B.Modules[i].Objective))
                    Record(state, client, B.CrippleTheDefenses, B.Modules[i].Event);

            if (state.SkybridgeSecured && Wants(player, B.FlanksForNothing, B.Skybridge))
                Record(state, client, B.FlanksForNothing, B.SkybridgeSecured);

            if (AllDown(state, north: true) && Wants(player, B.FlanksForNothing, B.NorthernPods))
                Record(state, client, B.FlanksForNothing, B.NorthernPodsDown);

            if (AllDown(state, north: false) && Wants(player, B.FlanksForNothing, B.SouthernPods))
                Record(state, client, B.FlanksForNothing, B.SouthernPodsDown);

            if (state.Chairs.All(live => live is { IsDown: true }) && Wants(player, B.HostileOvertaking, B.Machinery))
                Record(state, client, B.HostileOvertaking, B.MachineryDown);
        }

        private static bool AllDown(State state, bool north) =>
            Enumerable.Range(0, B.Pods.Length).Where(i => B.Pods[i].North == north).All(i => state.Pods[i] is { IsDown: true });

        #endregion

        #region Helpers

        /// <summary>The refinery's AFS take no harm, each one as it comes into the world again.</summary>
        private static void ProtectAfs(MapChannel map)
        {
            foreach (var creature in map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct())
                if (!creature.ImmuneToAllDamage && Array.IndexOf(B.Afs, creature.DbId) >= 0)
                    creature.ImmuneToAllDamage = true;
        }

        private static Creature Spawn(MapChannel map, uint dbId, Vector3 spot, Vector3 facingTo)
        {
            var creature = CreatureManager.Instance.CreateCreature(dbId, null);

            if (creature == null)
                return null;

            var at = NavMeshManager.SnapToGround(map, spot);
            var facing = Math.Atan2(facingTo.X - at.X, facingTo.Z - at.Z);
            CreatureManager.Instance.SetLocation(creature, at, facing, map.MapInfo.MapContextId);
            creature.LastYaw = (float)facing;
            creature.ImmuneToAllDamage = true;
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

            var key = (log.AssignmentId ?? string.Empty, missionId, B.Scenario, eventId);

            if (state.Told.Contains(key))
                return;

            if (MissionApplication.Instance.TryEmitScenarioProgressEvent(client, missionId, B.Scenario, eventId))
                state.Told.Add(key);
        }

        public static void Reset() => States.Clear();

        #endregion
    }
}
