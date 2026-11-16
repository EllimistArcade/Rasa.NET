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

    using T = Services.Preloader.TreebackCampContent;

    /// <summary>
    /// What the Treeback Camp (1397) does that its rows do not, in each copy of it, for the missions
    /// TreebackCampContent authors, as the wiki and Ten Ton Hammer tell them:
    ///
    ///  - Field Lieutenant Bagby at the beachhead. The forward base's computer turned on (a
    ///    <see cref="UseMs"/> use by anyone on Retake the Forward Base, with no Bane within
    ///    <see cref="ComputerClear"/> of it), he comes up to it <see cref="BagbyDelayMs"/> later,
    ///    and stays. A member who turned it on in another copy and still waits for him has it on.
    ///  - The gate: a Bane force field across the gap in the west wall, sealed. The Force Field
    ///    Controller in the south-west pillbox, thrown by anyone, takes it down for good in the
    ///    copy; so does a member who has done Open the Gate coming within <see cref="GateReach"/>
    ///    of it. Commander Kendrick and Field Officers Starks and Brooks land inside it then.
    ///  - Squad Leader Keen at the north bunker: within <see cref="SignalRadius"/> of him is the
    ///    signal found, within <see cref="CommandoRadius"/> the commando talked to.
    ///  - The leaders: Amuaggor in the pens and Cromak east of the gate from the start; the
    ///    generator station's control panel, used, brings Security Chief Thraggorth out with two of
    ///    his security; Base Commander Tagak comes to his command post once the three are dead in
    ///    the copy, or for a member already on Finishing Move. The copy remembers each death, and
    ///    each is its objective for every member on it, whenever they take it up; the badge off
    ///    Amuaggor likewise goes to anyone on Medical Alert Badge without one.
    ///  - The five power conduits, placed destructibles: each destroyed is its objective of
    ///    Sabotage (and one of its count) for everyone on it, and stays down in the copy.
    ///  - Captain Markinson lands in the generator station once a member who has done Sabotage or
    ///    Finishing Move, or is on Treeback Genocide, is within <see cref="StationReach"/> of it
    ///    and no Bane stands within <see cref="LandingClear"/> of where he lands.
    ///  - The Bane gas harvester in the pens: a use by anyone on Treeback Genocide holding the
    ///    NX-22 is the agent deployed, for everyone on it.
    ///  - The camp's AFS take no harm.
    /// What the copy has done goes to every member whose mission is waiting on it. Kept by the copy
    /// and not saved.
    /// </summary>
    public static class TreebackCamp
    {
        public const uint UseMs = 2_000;
        public const long BagbyDelayMs = 15_000;
        public const float ComputerClear = 20f, GateReach = 12f, SignalRadius = 30f, CommandoRadius = 5f, BagbyReach = 25f,
            StationReach = 40f, LandingClear = 35f;

        internal sealed class State
        {
            public bool Set;
            public Creature Bagby;
            public bool ComputerOn, BagbyForward, GateOpen, AgentDeployed;
            public long ComputerOnAt;
            public DynamicObject Computer, GateSwitch, Panel, Harvester;
            public ForceFields.Field Gate;
            public readonly PlacedDestructibles.Live[] Conduits = new PlacedDestructibles.Live[T.Conduits.Length];
            public Creature Kendrick, Starks, Brooks, Markinson;
            public readonly Dictionary<uint, Creature> Leaders = new();
            public readonly List<Creature> ThraggorthsGuard = new();

            /// <summary>The leaders dead in the copy.</summary>
            public readonly HashSet<uint> Dead = new();

            /// <summary>The assignments of Medical Alert Badge given the badge in the copy.</summary>
            public readonly HashSet<string> BadgeGiven = new();

            /// <summary>The events already recorded: the assignment, the mission, the objective waiting on it, the event.</summary>
            public readonly HashSet<(string Assignment, uint Mission, uint Objective, uint Event)> Told = new();
        }

        private static readonly ConditionalWeakTable<MapChannel, State> States = new();

        /// <summary>Milliseconds, for Bagby's walk up; a test's to set.</summary>
        public static Func<long> Now { get; set; } = () => Environment.TickCount64;

        public static bool IsCamp(MapChannel map) => map?.MapInfo?.MapContextId == T.Camp;

        internal static State StateOf(MapChannel map) => IsCamp(map) ? States.GetValue(map, _ => new State()) : null;

        public static Creature BagbyOf(MapChannel map) => StateOf(map)?.Bagby is { } bagby && Alive(bagby) ? bagby : null;

        public static bool BagbyAtForwardBase(MapChannel map) => StateOf(map)?.BagbyForward ?? false;

        public static bool ComputerOn(MapChannel map) => StateOf(map)?.ComputerOn ?? false;

        public static bool GateOpen(MapChannel map) => StateOf(map)?.GateOpen ?? false;

        public static bool AgentDeployed(MapChannel map) => StateOf(map)?.AgentDeployed ?? false;

        public static ForceFields.Field Gate(MapChannel map) => StateOf(map)?.Gate;

        public static DynamicObject Computer(MapChannel map) => StateOf(map)?.Computer;

        public static DynamicObject GateSwitch(MapChannel map) => StateOf(map)?.GateSwitch;

        public static DynamicObject Panel(MapChannel map) => StateOf(map)?.Panel;

        public static DynamicObject Harvester(MapChannel map) => StateOf(map)?.Harvester;

        public static IReadOnlyList<PlacedDestructibles.Live> Conduits(MapChannel map) => StateOf(map)?.Conduits ?? Array.Empty<PlacedDestructibles.Live>();

        /// <summary>The copy's leader of that creature row, alive; null if he is not.</summary>
        public static Creature Leader(MapChannel map, uint dbId) =>
            StateOf(map) is { } state && state.Leaders.TryGetValue(dbId, out var leader) && Alive(leader) ? leader : null;

        public static bool IsDead(MapChannel map, uint dbId) => StateOf(map)?.Dead.Contains(dbId) ?? false;

        public static Creature Kendrick(MapChannel map) => StateOf(map)?.Kendrick is { } kendrick && Alive(kendrick) ? kendrick : null;

        public static Creature Markinson(MapChannel map) => StateOf(map)?.Markinson is { } markinson && Alive(markinson) ? markinson : null;

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
            Leaders(map, state);

            var members = Members(map);

            if (members.Count == 0)
                return;

            var now = Now();
            Bagby(map, state, members, now);
            Gate(map, state, members);
            Tagak(map, state, members);
            Markinson(map, state, members);

            foreach (var client in members)
                Tell(state, client);
        }

        #region Set up

        private static void SetUp(MapChannel map, State state)
        {
            state.Set = true;

            state.Computer = PlaceObject(map, "treeback:computer", T.ComputerClass, T.ComputerAt, 0, UseObjectState.TsState0, 0);
            state.GateSwitch = PlaceObject(map, "treeback:gate-switch", T.GateSwitchClass, T.GateSwitchAt, T.GateSwitchYaw, UseObjectState.TsState0, T.GateSwitchName);
            state.Panel = PlaceObject(map, "treeback:panel", T.PanelClass, T.PanelAt, T.PanelYaw, UseObjectState.TsState0, T.PanelName);
            state.Harvester = PlaceObject(map, "treeback:harvester", T.HarvesterClass, T.HarvesterAt, 0, UseObjectState.IaStateActive, 0);

            state.Gate = ForceFields.Place(map, ForceFields.ClassOf("baneminor"), ForceFields.Side.B, T.GateAt, T.GateYaw, ForceFields.DefaultHealth);
            if (state.Gate != null)
                state.Gate.Sealed = true;

            for (var i = 0; i < T.Conduits.Length; i++)
            {
                var conduit = T.Conduits[i];
                state.Conduits[i] = PlacedDestructibles.PutDown(map, (EntityClasses)T.ConduitClass, NavMeshManager.SnapToGround(map, conduit.At),
                    conduit.Yaw, T.ConduitHitPoints);
            }

            state.Bagby = Spawn(map, T.Bagby, T.BagbyAt, T.ComputerAt, immune: true);

            Logger.WriteLog(LogType.Debug, $"Treeback Camp {map.InstanceId}: computer, gate, controller, panel, harvester and " +
                $"{state.Conduits.Count(live => live != null)} conduits put down.");
        }

        private static DynamicObject PlaceObject(MapChannel map, string key, uint classId, Vector3 at, float yaw, UseObjectState initial, uint name)
        {
            var existing = map.DynamicObjects.FirstOrDefault(obj => obj.ScenarioKey == key);

            if (existing != null)
                return existing;

            var obj = DynamicObjectManager.Instance.CreateScenarioDynamicObject(map, (EntityClasses)classId, at, yaw, key, true, UseMs);
            obj.StateId = initial;
            if (name != 0)
                obj.NameOverrideId = name;
            map.DynamicObjects.Add(obj);
            CellManager.Instance.AddToWorld(map, obj);
            obj.IsInWorld = true;
            return obj;
        }

        /// <summary>A conduit destroyed stays so: never due back.</summary>
        private static void KeepDown(State state)
        {
            foreach (var live in state.Conduits)
                if (live != null && live.IsDown && live.RespawnAt != long.MaxValue)
                    live.RespawnAt = long.MaxValue;
        }

        #endregion

        #region Bagby and the computer

        private static void Bagby(MapChannel map, State state, List<Client> members, long now)
        {
            // Turned on in another copy, and still waiting for him: on here too, and he is there.
            if (!state.ComputerOn && members.Any(client => Wants(client.Player, T.RetakeTheForwardBase, T.WaitForBagby)))
            {
                state.ComputerOn = true;
                state.ComputerOnAt = now - BagbyDelayMs;
                SwitchOn(map, state.Computer, UseObjectState.TsState1, 0);
            }

            if (!Alive(state.Bagby))
                state.Bagby = Spawn(map, T.Bagby, state.BagbyForward ? T.BagbyAtForwardBase : T.BagbyAt, T.ComputerAt, immune: true);

            if (state.BagbyForward || !state.ComputerOn || now - state.ComputerOnAt < BagbyDelayMs || state.Bagby == null)
                return;

            state.BagbyForward = true;
            var at = NavMeshManager.SnapToGround(map, T.BagbyAtForwardBase);

            if (BehaviorManager.Instance.RestoreScriptedPose(map, state.Bagby, at, Math.Atan2(T.ComputerAt.X - at.X, T.ComputerAt.Z - at.Z)))
            {
                state.Bagby.Controller.ScriptedMove = null;
                state.Bagby.Controller.CurrentAction = BehaviorManager.BehaviorActionIdle;
            }

            Logger.WriteLog(LogType.Debug, $"Treeback Camp {map.InstanceId}: Bagby is at the forward base.");
        }

        /// <summary>Whether a Bane stands within <paramref name="radius"/> of <paramref name="at"/>.</summary>
        public static bool BaneNear(MapChannel map, Vector3 at, float radius) =>
            map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct()
                .Any(creature => creature.TargetCategory == TargetCategory.Hostile && Alive(creature) &&
                    Vector3.Distance(creature.Position, at) <= radius);

        #endregion

        #region The gate

        private static void Gate(MapChannel map, State state, List<Client> members)
        {
            if (!state.GateOpen)
            {
                var by = members.FirstOrDefault(client => Done(client.Player, T.OpenTheGate) &&
                    (Vector3.Distance(client.Player.Position, T.GateWest) <= GateReach || Vector3.Distance(client.Player.Position, T.GateEast) <= GateReach));

                if (by != null)
                    OpenGate(map, state, by.Player);
            }

            if (state.GateOpen && state.Kendrick == null)
            {
                state.Kendrick = Spawn(map, T.Kendrick, T.KendrickAt, T.GateWest, immune: true);
                state.Starks = Spawn(map, T.Starks, T.StarksAt, T.GateWest, immune: true);
                state.Brooks = Spawn(map, T.Brooks, T.BrooksAt, T.GateWest, immune: true);
                Logger.WriteLog(LogType.Debug, $"Treeback Camp {map.InstanceId}: Kendrick lands inside the gate.");
            }
        }

        internal static void OpenGate(MapChannel map, State state, Actor by)
        {
            if (state.GateOpen)
                return;

            state.GateOpen = true;
            if (state.Gate is { IsDestroyed: false } gate)
                ForceFields.Damage(gate, gate.Health, by?.EntityId ?? 0);
            SwitchOn(map, state.GateSwitch, UseObjectState.TsState1, by?.EntityId ?? 0);
            Logger.WriteLog(LogType.Debug, $"Treeback Camp {map.InstanceId}: the gate's field is down.");
        }

        #endregion

        #region The leaders

        private static void Leaders(MapChannel map, State state)
        {
            foreach (var (dbId, at, facing) in new[] { (T.Amuaggor, T.AmuaggorAt, T.HarvesterAt), (T.Cromak, T.CromakAt, T.GateEast) })
                if (!state.Leaders.ContainsKey(dbId))
                    state.Leaders[dbId] = Spawn(map, dbId, at, facing, immune: false);

            foreach (var (dbId, leader) in state.Leaders.ToList())
            {
                if (leader == null || state.Dead.Contains(dbId))
                    continue;

                if (leader.State == CharacterState.Dead)
                {
                    state.Dead.Add(dbId);
                    Logger.WriteLog(LogType.Debug, $"Treeback Camp {map.InstanceId}: {leader.DbId} is dead.");
                }
                else if (!Alive(leader))
                    state.Leaders.Remove(dbId);    // gone from the world alive: put down again where he belongs
            }
        }

        /// <summary>Thraggorth comes out at the panel's call, with two of his security, unless he is out or dead.</summary>
        internal static bool LureThraggorth(MapChannel map, State state, Actor by)
        {
            if (state.Dead.Contains(T.Thraggorth) || Leader(map, T.Thraggorth) != null)
                return false;

            state.Leaders[T.Thraggorth] = Spawn(map, T.Thraggorth, T.ThraggorthAt, T.PanelAt, immune: false);
            state.ThraggorthsGuard.RemoveAll(guard => !Alive(guard));
            while (state.ThraggorthsGuard.Count < 2)
                state.ThraggorthsGuard.Add(Spawn(map, T.Security, T.ThraggorthAt + new Vector3(state.ThraggorthsGuard.Count == 0 ? 3f : -3f, 0, 2f), T.PanelAt, immune: false));

            SwitchOn(map, state.Panel, UseObjectState.TsState1, by?.EntityId ?? 0);
            Logger.WriteLog(LogType.Debug, $"Treeback Camp {map.InstanceId}: the panel brings Security Chief Thraggorth out.");
            return true;
        }

        private static void Tagak(MapChannel map, State state, List<Client> members)
        {
            if (state.Leaders.ContainsKey(T.Tagak))
                return;

            var lieutenants = new[] { T.Amuaggor, T.Cromak, T.Thraggorth }.All(state.Dead.Contains);

            if (!lieutenants && !members.Any(client => Wants(client.Player, T.FinishingMove, T.KillTheCommander)))
                return;

            state.Leaders[T.Tagak] = Spawn(map, T.Tagak, T.TagakAt, T.GateEast, immune: false);
            Logger.WriteLog(LogType.Debug, $"Treeback Camp {map.InstanceId}: Base Commander Tagak is at his command post.");
        }

        #endregion

        #region Markinson

        private static void Markinson(MapChannel map, State state, List<Client> members)
        {
            if (state.Markinson != null)
            {
                if (!Alive(state.Markinson))
                    state.Markinson = Spawn(map, T.Markinson, T.MarkinsonAt, T.PanelAt, immune: true);
                return;
            }

            if (!members.Any(client => Vector3.Distance(client.Player.Position, T.GeneratorStation) <= StationReach &&
                    (Done(client.Player, T.Sabotage) || Done(client.Player, T.FinishingMove) || client.Player.Missions.ContainsKey(T.TreebackGenocide))) ||
                BaneNear(map, T.MarkinsonAt, LandingClear))
                return;

            state.Markinson = Spawn(map, T.Markinson, T.MarkinsonAt, T.PanelAt, immune: true);
            Logger.WriteLog(LogType.Debug, $"Treeback Camp {map.InstanceId}: Markinson lands in the generator station.");
        }

        #endregion

        #region The objects' uses

        /// <summary>Whether a finished action is a use of one of the copy's objects (ActorActionManager).</summary>
        public static bool IsUseOf(MapChannel map, ActionData action) =>
            action != null && StateOf(map) is { } state && ObjectsOf(state).Any(obj => obj != null && obj.EntityId == action.SourceId);

        private static IEnumerable<DynamicObject> ObjectsOf(State state) => new[] { state.Computer, state.GateSwitch, state.Panel, state.Harvester };

        /// <summary>
        /// A use about to start (DynamicObjectManager.RequestUseObjectPacket): why it is refused, or
        /// null. The computer is for a player on Retake the Forward Base who has not turned it on,
        /// with the Thrax about it dead; the controller for anyone while the gate is shut; the panel
        /// for anyone while Thraggorth is neither out nor dead; the harvester for a player on Treeback
        /// Genocide who holds the NX-22, until it has it.
        /// </summary>
        public static PlayerMessage? Refusal(Client client, DynamicObject obj)
        {
            var map = client?.Player?.MapChannel;
            var state = StateOf(map);

            if (state == null || obj == null || !ObjectsOf(state).Contains(obj))
                return null;

            var player = client.Player;
            var usable =
                obj == state.Computer ? !state.ComputerOn && Wants(player, T.RetakeTheForwardBase, T.TurnOnTheComputer) && !BaneNear(map, T.ComputerAt, ComputerClear) :
                obj == state.GateSwitch ? !state.GateOpen :
                obj == state.Panel ? !state.Dead.Contains(T.Thraggorth) && Leader(map, T.Thraggorth) == null :
                !state.AgentDeployed && Wants(player, T.TreebackGenocide, T.DeployTheAgent) && Holds(player, T.NerveGasTemplate);

            return usable ? null : PlayerMessage.PmUseObjectNotUsable;
        }

        /// <summary>A finished use of one of the copy's objects: what it does.</summary>
        public static void UseRecovery(MapChannel map, ActionData action)
        {
            var state = StateOf(map);
            var obj = state == null ? null : ObjectsOf(state).FirstOrDefault(o => o != null && o.EntityId == action.SourceId);

            if (obj == null)
                return;

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

            Use(map, state, obj, client);
        }

        /// <summary>The use of one of the copy's objects, done.</summary>
        internal static void Use(MapChannel map, State state, DynamicObject obj, Client by)
        {
            if (obj == state.Computer)
            {
                state.ComputerOn = true;
                state.ComputerOnAt = Now();
                SwitchOn(map, obj, UseObjectState.TsState1, by.Player.EntityId);
                Logger.WriteLog(LogType.Debug, $"Treeback Camp {map.InstanceId}: {by.Player.FamilyName} turns the forward base's computer on.");
            }
            else if (obj == state.GateSwitch)
                OpenGate(map, state, by.Player);
            else if (obj == state.Panel)
                LureThraggorth(map, state, by.Player);
            else if (obj == state.Harvester)
            {
                state.AgentDeployed = true;
                SwitchOn(map, obj, UseObjectState.IaStateAccepted, by.Player.EntityId);
                Logger.WriteLog(LogType.Debug, $"Treeback Camp {map.InstanceId}: {by.Player.FamilyName} puts the NX-22 into the harvester.");
            }

            foreach (var client in Members(map).Prepend(by).Distinct())
                Tell(state, client);
        }

        private static void SwitchOn(MapChannel map, DynamicObject obj, UseObjectState to, ulong byEntityId)
        {
            if (obj == null || obj.StateId == to)
                return;

            obj.StateId = to;
            CellManager.Instance.CellCallMethod(map, obj, new UsePacket(byEntityId, to, 0));
        }

        #endregion

        #region The missions

        private static readonly (uint Leader, uint Mission, uint Objective, uint Event)[] LeaderEvents =
        {
            (T.Amuaggor, T.CutOffTheHead, T.KillResearcher, T.ResearcherDead),
            (T.Cromak, T.CutOffTheHead, T.KillHunterLord, T.HunterLordDead),
            (T.Thraggorth, T.CutOffTheHead, T.KillSecurityChief, T.SecurityChiefDead),
            (T.Tagak, T.FinishingMove, T.KillTheCommander, T.CommanderDead)
        };

        /// <summary>What the copy has done, to the member's missions that wait on it.</summary>
        private static void Tell(State state, Client client)
        {
            var player = client.Player;

            if (state.ComputerOn)
                Record(state, client, T.RetakeTheForwardBase, T.TurnOnTheComputer, T.ComputerOn);

            if (state.BagbyForward && Alive(state.Bagby) && Vector3.Distance(player.Position, state.Bagby.Position) <= BagbyReach)
                Record(state, client, T.RetakeTheForwardBase, T.WaitForBagby, T.BagbyArrived);

            if (state.GateOpen)
                Record(state, client, T.OpenTheGate, T.ShutOffTheField, T.GateOpen);

            var keen = Vector3.Distance(player.Position, T.KeenAt);
            if (keen <= SignalRadius)
                Record(state, client, T.CheckTheRidge, T.FindTheSignal, T.SignalFound);
            if (keen <= CommandoRadius)
                Record(state, client, T.CheckTheRidge, T.TalkToTheCommando, T.CommandoFound);

            foreach (var (leader, mission, objective, eventId) in LeaderEvents)
                if (state.Dead.Contains(leader))
                    Record(state, client, mission, objective, eventId);

            for (var i = 0; i < T.Conduits.Length; i++)
                if (state.Conduits[i] is { IsDown: true })
                {
                    Record(state, client, T.Sabotage, T.Conduits[i].Objective, (uint)i + 1);
                    Record(state, client, T.Sabotage, T.AllConduits, (uint)i + 1);
                }

            if (state.AgentDeployed)
                Record(state, client, T.TreebackGenocide, T.DeployTheAgent, T.AgentDeployed);

            Badge(state, client);
        }

        /// <summary>Amuaggor dead in the copy: his badge, for a member on Medical Alert Badge who has none, once an assignment.</summary>
        private static void Badge(State state, Client client)
        {
            var player = client.Player;

            if (!state.Dead.Contains(T.Amuaggor) || !Wants(player, T.MedicalAlertBadge, T.GetTheBadge) || Holds(player, T.BadgeTemplate) ||
                !player.Missions.TryGetValue(T.MedicalAlertBadge, out var log) || !state.BadgeGiven.Add(log.AssignmentId ?? string.Empty))
                return;

            var item = ItemManager.Instance.CreateFromTemplateId(T.BadgeTemplate, 1);

            if (item == null || InventoryManager.Instance.GrantItemToInventory(client, item) == null)
            {
                state.BadgeGiven.Remove(log.AssignmentId ?? string.Empty);
                Logger.WriteLog(LogType.Error, $"Treeback Camp: Amuaggor's badge could not go to {player.FamilyName}.");
            }
        }

        #endregion

        #region Helpers

        /// <summary>The camp's AFS take no harm, each one as it comes into the world again.</summary>
        private static void ProtectAfs(MapChannel map)
        {
            foreach (var creature in map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct())
                if (!creature.ImmuneToAllDamage && Array.IndexOf(T.Afs, creature.DbId) >= 0)
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

        private static bool Holds(Manifestation player, uint template) =>
            player.Inventory.PersonalInventory.Where(id => id != 0).Select(EntityManager.Instance.GetItem)
                .Any(item => item != null && item.ItemTemplateId == template);

        /// <summary>The event, once an assignment and objective, to a member whose objective waits on it.</summary>
        private static void Record(State state, Client client, uint missionId, uint objectiveId, uint eventId)
        {
            if (!Wants(client.Player, missionId, objectiveId) || !client.Player.Missions.TryGetValue(missionId, out var log))
                return;

            var key = (log.AssignmentId ?? string.Empty, missionId, objectiveId, eventId);

            if (state.Told.Contains(key))
                return;

            if (MissionApplication.Instance.TryEmitScenarioProgressEvent(client, missionId, T.Scenario, eventId))
                state.Told.Add(key);
        }

        public static void Reset() => States.Clear();

        #endregion
    }
}
