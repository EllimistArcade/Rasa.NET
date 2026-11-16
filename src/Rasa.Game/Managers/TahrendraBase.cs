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
    using Rasa.Missions.Scenes;
    using Structures;
    using Structures.World;

    using T = Services.Preloader.TahrendraBaseContent;

    /// <summary>
    /// What Tahrendra Base (2125) does that its rows do not, in each copy of it - a squad's
    /// instance, or the map's own channel - for the missions TahrendraBaseContent authors, as the
    /// wiki and Ten Ton Hammer's walkthrough tell it:
    ///
    ///  - Captain Tillery stands beside the field medic at the way in, and in the Security Check
    ///    Point room once both Guard Barracks are sealed in the copy, or for a member who has done
    ///    Break on Through.
    ///  - The Guard Lounge's two crates give a member on Break on Through a Salvaged Detonator
    ///    while they hold fewer than <see cref="T.DetonatorsWanted"/>, and are empty for
    ///    <see cref="CrateEmptyMinMs"/> to <see cref="CrateEmptyMaxMs"/>.
    ///  - At each barracks' door a bomb takes a detonator (PlacedUsables); gone off, a door
    ///    (ForceFields, sealed) shuts the barracks and its pool sends no more. Both sealed is the
    ///    objective for everyone on it.
    ///  - The Bane CommLink Terminal in Wormhole Security is an object conversation (package 1634).
    ///  - The AFS teleporter across from Security Station 2's door is switched on by a member whose
    ///    objective it is: Specialist Donnaway beams in beside it. The red light by the door takes
    ///    the Biohazard Charge from a member on its objective; <see cref="FuseMs"/> later it goes
    ///    off, every Bane in the station loses <see cref="GasShare"/> of its health, and the
    ///    station's Bane transporter is wrecked. Standing, the transporter beams a soldier in every
    ///    <see cref="BeamEveryMs"/> while a member is within <see cref="BeamRange"/>, no more than
    ///    <see cref="BeamLimit"/> of its own about at once.
    ///  - The four Warden Bots on the way up to Upper Block Security: a hit from a member on
    ///    Bringing the House Down who holds the Bot Virus turns one (no harm done): it follows them
    ///    and fights for them. <see cref="T.RobotsWanted"/> turned by a member is their objective.
    ///    One whose member has left goes home and is neutral again.
    ///  - Upper Block Security's pools all dead at once is it secured, for good.
    ///  - What Goes Around: Donnaway waits at the wormhole room's door, follows the nearest member
    ///    on the escort within <see cref="PickupRadius"/>, is put beside them more than
    ///    <see cref="WarpDistance"/> away or where she cannot walk to them, and takes no harm. On
    ///    the room's mark (<see cref="DeliverRadius"/>) she goes to work: Dominator Ryxnl comes
    ///    through the main door with soldiers, Hunters on the balconies; the second wave
    ///    <see cref="Wave2Ms"/> in, the third <see cref="Wave3Ms"/> in, each dead its objective; the
    ///    upload done <see cref="UploadMs"/> in with every wave dead, and the Eloh's hologram
    ///    stands by the wormhole.
    /// What the copy has done goes to every member whose mission waits on it. Kept by the copy and
    /// not saved; a member's mission carries Tillery's place, Donnaway's delivery and the upload
    /// into a copy made again.
    /// </summary>
    public static class TahrendraBase
    {
        public const int UseWindupMs = 2_000;
        public const long CrateEmptyMinMs = 60_000, CrateEmptyMaxMs = 120_000;
        public const long FuseMs = 5_000;
        public const float GasShare = 0.6f, GasRadius = 24f;
        public const float BeamRange = 30f;
        public const long BeamEveryMs = 45_000;
        public const int BeamLimit = 2;
        public const float PickupRadius = 15f, DeliverRadius = 6f, WarpDistance = 40f;
        public const long Wave2Ms = 60_000, Wave3Ms = 120_000, UploadMs = 180_000;
        public const long BotBackMs = 120_000;
        public const float BotAggroRange = 20f;
        public const long RetryMs = 10_000;

        public enum Kind { Crate, Teleporter, BioSite }

        internal sealed class Usable
        {
            public Kind Kind;
            public int Index;
            public DynamicObject Object;
        }

        /// <summary>Donnaway's part: away, by the teleporter, at the wormhole room's door, following, at work, done.</summary>
        public enum Stage { Away, AtTeleporter, AtDoor, Escorting, Working, Done }

        internal sealed class Bot
        {
            public Creature Creature;
            public ulong Master;
            public long BackAt;
        }

        internal sealed class State
        {
            public bool Set;
            public Creature Tillery;
            public bool TilleryMoved;

            public readonly List<Usable> Usables = new();
            public readonly long[] CrateBackAt = new long[T.Crates.Length];
            public readonly PlacedUsables.Live[] BarracksBombs = new PlacedUsables.Live[T.GuardBarracks.Length];
            public readonly ForceFields.Field[] BarracksDoors = new ForceFields.Field[T.GuardBarracks.Length];
            public readonly bool[] Sealed = new bool[T.GuardBarracks.Length];
            public DynamicObject Terminal;

            public bool TeleporterOn;
            public long BioFuseAt;
            public ulong BioSetBy;
            public bool BioBombSet;

            public PlacedDestructibles.Live Transporter;
            public readonly List<Creature> Beamed = new();
            public long NextBeamAt;

            public readonly Bot[] Bots = T.WardenBots.Select(_ => new Bot()).ToArray();
            public readonly Dictionary<uint, HashSet<int>> Turned = new();

            public bool UpperBlockSecured;

            public Creature Donnaway;
            public Stage Stage;
            public ulong DonnawayLead;

            public long UploadFrom;
            public readonly List<Creature>[] Waves = { new(), new(), new() };
            public readonly bool[] WaveSent = new bool[3];
            public bool UploadDone;
            public Creature Hologram;

            /// <summary>Events already given, by player, assignment and event: once; one that changed nothing is tried again later.</summary>
            public readonly Dictionary<string, long> Given = new();
        }

        private static readonly ConditionalWeakTable<MapChannel, State> States = new();

        /// <summary>The clock; a test's to replace.</summary>
        public static Func<long> Now { get; set; } = () => Environment.TickCount64;

        /// <summary>A random number from the first to the second, both in; a test's to replace.</summary>
        public static Func<long, long, long> Roll { get; set; } = (min, max) => Random.Shared.NextInt64(min, max + 1);

        public static bool IsBase(MapChannel map) => map?.MapInfo?.MapContextId == T.Tahrendra;

        internal static State StateOf(MapChannel map) => IsBase(map) ? States.GetValue(map, _ => new State()) : null;

        /// <summary>From the map channel worker.</summary>
        public static void Worker(MapChannel map)
        {
            var state = StateOf(map);

            if (state == null)
                return;

            var now = Now();

            if (!state.Set)
                SetUp(map, state);

            ProtectAfs(map);
            Crates(map, state, now);
            Barracks(map, state);
            BioBomb(map, state, now);
            KeepDown(state);

            var members = Members(map);

            Tillery(map, state, members);
            Transporter(map, state, members, now);
            Bots(map, state, members, now);
            UpperBlock(map, state);
            Donnaway(map, state, members, now);
            Upload(map, state, members, now);

            foreach (var client in members)
                Tell(state, client, now);
        }

        #region Set up

        private static void SetUp(MapChannel map, State state)
        {
            state.Set = true;

            for (var i = 0; i < T.Crates.Length; i++)
                Add(state, Kind.Crate, i, Place(map, $"tahrendra:crate-{i}", T.CrateClass, Ground(map, T.Crates[i].At), T.Crates[i].Yaw,
                    UseObjectState.TdStateClosed));

            for (var i = 0; i < T.GuardBarracks.Length; i++)
                state.BarracksBombs[i] = Bomb(map, T.GuardBarracks[i].BombAt, $"Tahrendra Base: the bomb at {T.GuardBarracks[i].Name}' door");

            state.Terminal = Place(map, "tahrendra:terminal", T.KeyboardClass, Ground(map, T.Terminal), 0, UseObjectState.TsState0,
                obj => obj.MissionConversation = new SceneObjectConversation(T.BringingTheHouseDown, T.SecurityConsole, T.ConsolePackage, T.SecurityConsole));

            Add(state, Kind.Teleporter, 0, Place(map, "tahrendra:afs-teleporter", T.TeleporterClass, Ground(map, T.AfsTeleporter), 1.5707964,
                UseObjectState.TsState0));
            Add(state, Kind.BioSite, 0, Place(map, "tahrendra:bio-site", T.BombClass, Ground(map, T.BioSite), 0, UseObjectState.BombStateDisarmed));

            state.Transporter = PlacedDestructibles.PutDown(map, (EntityClasses)T.TransporterClass, Ground(map, T.Transporter), 0, T.TransporterHitPoints);

            for (var i = 0; i < state.Bots.Length; i++)
                state.Bots[i].Creature = SpawnBot(map, i);

            Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: {state.Usables.Count} usables, " +
                $"{state.BarracksBombs.Count(bomb => bomb != null)} barracks bombs, the terminal, the transporter and " +
                $"{state.Bots.Count(bot => bot.Creature != null)} Warden Bots put down.");
        }

        private static void Add(State state, Kind kind, int index, DynamicObject obj) =>
            state.Usables.Add(new Usable { Kind = kind, Index = index, Object = obj });

        /// <summary>A usable of the copy, made as <paramref name="init"/> has it before anyone is shown it.</summary>
        private static DynamicObject Place(MapChannel map, string key, uint classId, Vector3 position, double rotation, UseObjectState initial,
            Action<DynamicObject> init = null)
        {
            var existing = map.DynamicObjects.FirstOrDefault(obj => obj.ScenarioKey == key);

            if (existing != null)
                return existing;

            var obj = DynamicObjectManager.Instance.CreateScenarioDynamicObject(map, (EntityClasses)classId, position, rotation, key, true, UseWindupMs);
            obj.StateId = initial;
            init?.Invoke(obj);
            map.DynamicObjects.Add(obj);
            CellManager.Instance.AddToWorld(map, obj);
            obj.IsInWorld = true;
            return obj;
        }

        /// <summary>A barracks' bomb: armed with a Salvaged Detonator, and its blast only a door's.</summary>
        private static PlacedUsables.Live Bomb(MapChannel map, Vector3 at, string comment)
        {
            var ground = Ground(map, at);
            var bomb = PlacedUsables.PutDown(map, new WorldUsableEntry
            {
                ClassId = T.BombClass,
                PosX = ground.X,
                PosY = ground.Y,
                PosZ = ground.Z,
                Usable = 1,
                ItemClassId = T.DetonatorClass,
                ItemCount = 1,
                DamageMin = 1,
                DamageMax = 1,
                Radius = 1,
                Comment = comment
            }, out var why);

            if (bomb == null)
                Logger.WriteLog(LogType.Error, $"Tahrendra Base {map.InstanceId}: no bomb at {at}: {why}.");

            return bomb;
        }

        /// <summary>What is destroyed stays so: never due back.</summary>
        private static void KeepDown(State state)
        {
            if (state.Transporter is { IsDown: true } transporter && transporter.RespawnAt != long.MaxValue)
                transporter.RespawnAt = long.MaxValue;
        }

        #endregion

        #region Use

        /// <summary>Whether a finished use is of one of the copy's usables (ActorActionManager.PerformRecovery).</summary>
        public static bool IsUseOf(MapChannel map, ActionData action) =>
            action != null && StateOf(map) is { } state && state.Usables.Any(usable => usable.Object.EntityId == action.SourceId);

        /// <summary>
        /// A use about to start (DynamicObjectManager.RequestUseObjectPacket): the message it is
        /// refused with, or null. An emptied crate is empty; the teleporter is for a member whose
        /// objective it is, once; the red light for a member with the charge on its objective.
        /// </summary>
        public static PlayerMessage? Refusal(Client client, DynamicObject obj)
        {
            var state = StateOf(client?.Player?.MapChannel);
            var usable = UsableOf(state, obj);

            if (usable == null)
                return null;

            var player = client.Player;

            return usable.Kind switch
            {
                Kind.Crate when state.CrateBackAt[usable.Index] != 0 => PlayerMessage.PmUseObjectNotUsable,
                Kind.Teleporter when state.TeleporterOn || !Wants(player, T.BringingTheHouseDown, T.Teleporter) => PlayerMessage.PmUseObjectNotUsable,
                Kind.BioSite when state.BioBombSet || state.BioFuseAt != 0 || !Wants(player, T.BringingTheHouseDown, T.BioBomb) =>
                    PlayerMessage.PmUseObjectNotUsable,
                Kind.BioSite when InventoryManager.Instance.CountItemsByClass(client, (EntityClasses)T.ChargeClass, includeMissionItems: true) == 0 =>
                    PlayerMessage.PmMissingReqItem,
                _ => null
            };
        }

        /// <summary>A finished use of one of the copy's usables: what it does.</summary>
        public static void UseRecovery(MapChannel map, ActionData action)
        {
            var state = StateOf(map);
            var usable = state?.Usables.FirstOrDefault(entry => entry.Object.EntityId == action.SourceId);

            if (usable == null)
                return;

            var obj = usable.Object;
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

            var now = Now();

            switch (usable.Kind)
            {
                case Kind.Crate:
                    OpenCrate(map, state, usable, client, now);
                    break;
                case Kind.Teleporter:
                    SwitchOn(map, state, usable, client, now);
                    break;
                case Kind.BioSite:
                    SetCharge(map, state, usable, client, now);
                    break;
            }
        }

        private static Usable UsableOf(State state, DynamicObject obj) =>
            state == null || obj == null ? null : state.Usables.FirstOrDefault(usable => ReferenceEquals(usable.Object, obj));

        private static void Show(MapChannel map, DynamicObject obj, UseObjectState next, ulong actor)
        {
            obj.StateId = next;
            CellManager.Instance.CellCallMethod(map, obj, new UsePacket(actor, next, 0));
        }

        private static void SetEnabled(MapChannel map, DynamicObject obj, bool enabled)
        {
            if (obj == null || obj.IsEnabled == enabled)
                return;

            DynamicObjectManager.Instance.SetScenarioInteractionEnabled(map, obj, enabled);
        }

        #endregion

        #region Break on Through

        private static uint Detonators(Client client) =>
            InventoryManager.Instance.CountItemsByClass(client, (EntityClasses)T.DetonatorClass);

        private static void OpenCrate(MapChannel map, State state, Usable usable, Client client, long now)
        {
            if (!Active(client.Player, T.BreakOnThrough) || state.Sealed.All(done => done) || Detonators(client) >= T.DetonatorsWanted)
            {
                CommunicatorManager.Instance.SystemMessage(client, "Bane stores. Nothing here you need.");
                return;
            }

            var item = ItemManager.Instance.CreateFromTemplateId(T.DetonatorTemplate, 1);

            if (item == null || InventoryManager.Instance.GrantItemToInventory(client, item) == null)
            {
                Logger.WriteLog(LogType.Error, $"Tahrendra Base: a Salvaged Detonator could not go to {client.Player.FamilyName}.");
                return;
            }

            state.CrateBackAt[usable.Index] = now + Roll(CrateEmptyMinMs, CrateEmptyMaxMs);
            Show(map, usable.Object, UseObjectState.TdStateOpened, client.Player.EntityId);
            SetEnabled(map, usable.Object, false);
        }

        /// <summary>An emptied crate has a detonator again once its time is up.</summary>
        private static void Crates(MapChannel map, State state, long now)
        {
            foreach (var usable in state.Usables.Where(entry => entry.Kind == Kind.Crate))
            {
                var backAt = state.CrateBackAt[usable.Index];

                if (backAt == 0 || now < backAt)
                    continue;

                state.CrateBackAt[usable.Index] = 0;
                Show(map, usable.Object, UseObjectState.TdStateClosed, 0);
                SetEnabled(map, usable.Object, true);
            }
        }

        /// <summary>Whether a crate of the copy is empty now, for a test or a GM.</summary>
        public static bool CrateEmpty(MapChannel map, int crate) => StateOf(map)?.CrateBackAt[crate] != 0;

        /// <summary>A barracks' bomb gone off seals it.</summary>
        private static void Barracks(MapChannel map, State state)
        {
            for (var i = 0; i < T.GuardBarracks.Length; i++)
            {
                var bomb = state.BarracksBombs[i];

                if (state.Sealed[i] || bomb == null || bomb.State != UseObjectState.BombStateDetonated)
                    continue;

                Seal(map, state, i);
            }
        }

        /// <summary>The barracks' door shut for good, and its pool sends no more.</summary>
        internal static void Seal(MapChannel map, State state, int index)
        {
            var barracks = T.GuardBarracks[index];

            state.Sealed[index] = true;
            var door = ForceFields.Place(map, ForceFields.ClassOf("branndoor"), ForceFields.Side.B, barracks.Door, barracks.Yaw, 1);
            door.Sealed = true;
            state.BarracksDoors[index] = door;

            foreach (var pool in map.SpawnPools.Where(pool => pool.DbId == barracks.Pool))
                pool.Quieted = true;

            if (state.BarracksBombs[index]?.Object is { } obj)
                DynamicObjectManager.Instance.SetEnabled(obj, false);

            foreach (var member in Members(map).Where(member => Vector3.Distance(member.Player.Position, barracks.Door) <= 60f))
                CommunicatorManager.Instance.SystemMessage(member, $"The blast brings the door of {barracks.Name} down: its Bane are sealed in.");

            Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: {barracks.Name} sealed.");
        }

        /// <summary>Whether the copy's barracks are sealed, in GuardBarracks' order; for a test.</summary>
        public static IReadOnlyList<bool> Sealed(MapChannel map) => StateOf(map)?.Sealed ?? Array.Empty<bool>();

        public static IReadOnlyList<PlacedUsables.Live> BarracksBombs(MapChannel map) =>
            StateOf(map)?.BarracksBombs ?? Array.Empty<PlacedUsables.Live>();

        public static IReadOnlyList<ForceFields.Field> BarracksDoors(MapChannel map) =>
            StateOf(map)?.BarracksDoors ?? Array.Empty<ForceFields.Field>();

        /// <summary>
        /// Tillery beside the medic; in the check point room once both barracks are sealed, or a
        /// member has Break on Through done.
        /// </summary>
        private static void Tillery(MapChannel map, State state, List<Client> members)
        {
            var moved = state.TilleryMoved || state.Sealed.All(done => done) || members.Any(client => Done(client.Player, T.BreakOnThrough));
            var spot = moved ? T.TilleryAtCheckPoint : T.TilleryAtMedic;

            if (!Alive(state.Tillery))
            {
                state.Tillery = Spawn(map, T.Tillery, spot, 0f, moved ? T.CheckPoint : T.Arrival, immune: true);
                state.TilleryMoved = moved;
                return;
            }

            if (moved == state.TilleryMoved)
                return;

            state.TilleryMoved = true;
            Stand(map, state.Tillery, Ground(map, spot), T.CheckPoint);
            Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: Captain Tillery is in the Security Check Point room.");
        }

        /// <summary>The copy's Tillery, alive; null if he is not.</summary>
        public static Creature TilleryOf(MapChannel map) => StateOf(map)?.Tillery is { } tillery && Alive(tillery) ? tillery : null;

        #endregion

        #region Bringing the House Down

        private static void SwitchOn(MapChannel map, State state, Usable usable, Client client, long now)
        {
            state.TeleporterOn = true;
            Show(map, usable.Object, UseObjectState.TsState1, client.Player.EntityId);
            SetEnabled(map, usable.Object, false);
            Record(state, client, T.BringingTheHouseDown, T.TeleporterOn, now);

            if (state.Stage == Stage.Away)
            {
                state.Stage = Stage.AtTeleporter;

                if (!Alive(state.Donnaway))
                    state.Donnaway = Spawn(map, T.Donnaway, T.DonnawayBeamsIn, 0f, T.BioSite, immune: true);

                CommunicatorManager.Instance.SystemMessage(client, "The teleporter hums to life, and Specialist Donnaway beams in.");
            }

            Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: {client.Player.FamilyName} switches the AFS teleporter on.");
        }

        /// <summary>Whether the copy's AFS teleporter is on, for a test.</summary>
        public static bool TeleporterOn(MapChannel map) => StateOf(map)?.TeleporterOn == true;

        private static void SetCharge(MapChannel map, State state, Usable usable, Client client, long now)
        {
            state.BioFuseAt = now + FuseMs;
            state.BioSetBy = client.Player.EntityId;
            Show(map, usable.Object, UseObjectState.BombStateArmed, client.Player.EntityId);
            SetEnabled(map, usable.Object, false);
            Record(state, client, T.BringingTheHouseDown, T.BioBombSet, now);
            CommunicatorManager.Instance.SystemMessage(client, "The Biohazard Charge clamps to the lock and begins to vent.");
        }

        /// <summary>The charge's fuse run out: the gas through Security Station 2, and its transporter wrecked.</summary>
        private static void BioBomb(MapChannel map, State state, long now)
        {
            if (state.BioBombSet || state.BioFuseAt == 0 || now < state.BioFuseAt)
                return;

            state.BioBombSet = true;
            var by = EntityManager.Instance.Players.TryGetValue(state.BioSetBy, out var player) ? player : null;

            if (state.Usables.FirstOrDefault(usable => usable.Kind == Kind.BioSite)?.Object is { } site)
                Show(map, site, UseObjectState.BombStateDetonated, state.BioSetBy);

            var gassed = 0;

            foreach (var creature in Creatures(map).Where(InStation2))
            {
                if (creature.TargetCategory != TargetCategory.Hostile || !creature.Attributes.TryGetValue(Attributes.Health, out var health))
                    continue;

                var amount = (int)Math.Max(1, health.NormalMax * GasShare);
                ActorManager.Instance.Damage(map, creature, amount, null, out _, DamageType.Virulent);
                gassed++;

                if (health.Current <= 0 && creature.State != CharacterState.Dead)
                    CreatureManager.Instance.HandleCreatureKill(map, creature, by);
            }

            if (state.Transporter is { IsDown: false } transporter)
                PlacedDestructibles.Destroy(transporter, by);

            foreach (var member in Members(map).Where(member => InStation2(member.Player) || Vector3.Distance(member.Player.Position, T.BioSite) <= 20f))
                CommunicatorManager.Instance.SystemMessage(member, "A green haze pours through Security Station 2. The Bane inside reel.");

            Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: the Biohazard Charge goes off; {gassed} Bane gassed.");
        }

        /// <summary>Whether the copy's Biohazard Charge has gone off, for a test.</summary>
        public static bool BioBombGoneOff(MapChannel map) => StateOf(map)?.BioBombSet == true;

        private static bool InStation2(Actor actor) =>
            Vector2.Distance(new Vector2(actor.Position.X, actor.Position.Z), new Vector2(T.Station2.X, T.Station2.Z)) <= GasRadius &&
            Math.Abs(actor.Position.Y - T.Station2.Y) <= 8f;

        /// <summary>The station's transporter beams a soldier in at the nearest member within reach.</summary>
        private static void Transporter(MapChannel map, State state, List<Client> members, long now)
        {
            var live = state.Transporter;

            if (live == null || live.IsDown)
                return;

            state.Beamed.RemoveAll(creature => !Alive(creature));

            var at = live.Object.Position;
            var near = members.Where(client => Vector3.Distance(client.Player.Position, at) <= BeamRange).ToList();

            if (near.Count == 0 || now < state.NextBeamAt || state.Beamed.Count >= BeamLimit)
                return;

            state.NextBeamAt = now + BeamEveryMs;

            if (Spawn(map, T.Soldier, at, 3f, Nearest(near, at).Player.Position) is not { } bane)
                return;

            bane.Hate.Ensure(Nearest(near, bane.Position).Player.EntityId, Threat.NoticedThreat);
            state.Beamed.Add(bane);
        }

        public static PlacedDestructibles.Live TransporterOf(MapChannel map) => StateOf(map)?.Transporter;

        /// <summary>Upper Block Security's pools, each with its creatures out and all of them dead at once: secured, for good.</summary>
        private static void UpperBlock(MapChannel map, State state)
        {
            if (state.UpperBlockSecured)
                return;

            var pools = T.UpperBlockPools.Select(id => map.SpawnPools.FirstOrDefault(pool => pool.DbId == id)).ToList();

            if (pools.Any(pool => pool == null || pool.DeadCreatures == 0 || pool.AliveCreatures > 0 || pool.QueuedCreatures > 0 ||
                    pool.DropshipQueue > 0))
                return;

            state.UpperBlockSecured = true;
            Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: Upper Block Security is secured.");
        }

        /// <summary>Whether Upper Block Security is secured in the copy.</summary>
        public static bool UpperBlockSecured(MapChannel map) => StateOf(map)?.UpperBlockSecured == true;

        #endregion

        #region The Warden Bots

        private static Creature SpawnBot(MapChannel map, int index) =>
            Spawn(map, T.WardenBot, T.WardenBots[index], 0f, T.Station3);

        /// <summary>The copy's Warden Bots, in WardenBots' order, alive or not; for a test.</summary>
        public static IReadOnlyList<Creature> BotsOf(MapChannel map) =>
            StateOf(map)?.Bots.Select(bot => bot.Creature).ToList() ?? new List<Creature>();

        /// <summary>The bots a player has turned in the copy.</summary>
        public static int TurnedBy(MapChannel map, Manifestation player) =>
            StateOf(map) is { } state && player != null && state.Turned.TryGetValue(player.Id, out var turned) ? turned.Count : 0;

        /// <summary>
        /// A hit about to land (ActorManager.Damage): when it is a member's on Bringing the House
        /// Down, holding the Bot Virus, on one of the copy's Warden Bots no one has, the bot is
        /// theirs and takes nothing. True when the hit is spent so.
        /// </summary>
        public static bool Turns(MapChannel map, Actor source, Actor target)
        {
            if (target is not Creature creature || source is not Manifestation player || StateOf(map) is not { } state)
                return false;

            var index = Array.FindIndex(state.Bots, bot => ReferenceEquals(bot.Creature, creature));

            if (index < 0 || state.Bots[index].Master != 0 || !Active(player, T.BringingTheHouseDown))
                return false;

            var client = map.ClientList?.FirstOrDefault(c => c?.Player == player);

            if (client == null || InventoryManager.Instance.CountItemsByClass(client, (EntityClasses)T.VirusClass, includeMissionItems: true) == 0)
                return false;

            Turn(map, state, index, player);
            return true;
        }

        private static void Turn(MapChannel map, State state, int index, Manifestation player)
        {
            var bot = state.Bots[index];
            var creature = bot.Creature;

            bot.Master = player.EntityId;
            creature.TargetCategory = TargetCategory.Friendly;
            creature.AggroRange = BotAggroRange;
            creature.MasterEntityId = player.EntityId;
            creature.Stance = MinionStance.Aggressive;   // a creature with a master only scans when aggressive
            creature.Hate.Clear();

            CellManager.Instance.CellCallMethod(creature, new TargetCategoryPacket(TargetCategory.Friendly));
            BehaviorManager.Instance.StopFighting(creature);
            BehaviorManager.Instance.SetActionFollow(creature, player.EntityId);
            creature.Controller.ActionFollow.AssistTargetId = player.EntityId;

            // The rest forget it was ever against them.
            foreach (var other in Creatures(map).Where(other => other != creature))
                other.Hate.Remove(creature.EntityId);

            if (!state.Turned.TryGetValue(player.Id, out var turned))
                state.Turned[player.Id] = turned = new HashSet<int>();

            turned.Add(index);
            Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: Warden Bot {index} turned by {player.FamilyName} ({turned.Count}).");
        }

        /// <summary>A bot whose member has gone goes home, neutral again; a dead one, unturned, is back a while later.</summary>
        private static void Bots(MapChannel map, State state, List<Client> members, long now)
        {
            for (var i = 0; i < state.Bots.Length; i++)
            {
                var bot = state.Bots[i];

                if (!Alive(bot.Creature))
                {
                    if (bot.BackAt == 0)
                        bot.BackAt = now + BotBackMs;

                    if (now < bot.BackAt)
                        continue;

                    bot.BackAt = 0;
                    bot.Master = 0;
                    bot.Creature = SpawnBot(map, i);
                    continue;
                }

                if (bot.Master == 0 || members.Any(client => client.Player.EntityId == bot.Master))
                    continue;

                var creature = bot.Creature;
                bot.Master = 0;
                creature.TargetCategory = TargetCategory.Neutral;
                creature.MasterEntityId = 0;
                creature.Stance = MinionStance.Defensive;
                creature.Hate.Clear();
                CellManager.Instance.CellCallMethod(creature, new TargetCategoryPacket(TargetCategory.Neutral));
                BehaviorManager.Instance.StopFighting(creature);
                creature.Controller.ActionFollow.FollowTargetId = 0;
                creature.Controller.ActionFollow.AssistTargetId = 0;
                creature.Controller.CurrentAction = BehaviorManager.BehaviorActionIdle;
            }
        }

        #endregion

        #region What Goes Around

        private static bool Escorting(Manifestation player) => Wants(player, T.WhatGoesAround, T.EscortDonnaway);

        /// <summary>The copy's Donnaway, alive; null if she is not.</summary>
        public static Creature DonnawayOf(MapChannel map) => StateOf(map)?.Donnaway is { } donnaway && Alive(donnaway) ? donnaway : null;

        public static Stage DonnawayStage(MapChannel map) => StateOf(map)?.Stage ?? Stage.Away;

        private static void Donnaway(MapChannel map, State state, List<Client> members, long now)
        {
            // A member with her delivered already, in a copy before this one: she is at work.
            if (state.Stage < Stage.Working && members.Any(client => Done(client.Player, T.WhatGoesAround, T.EscortDonnaway)))
            {
                EnsureDonnaway(map, state, T.DonnawayAtWork, T.Wormhole);
                state.Stage = Stage.Working;
                state.UploadFrom = now;
            }

            if (state.Stage < Stage.AtDoor && members.Any(client => Active(client.Player, T.WhatGoesAround)))
            {
                EnsureDonnaway(map, state, T.RendezvousAt, T.WormholeMark);
                state.Stage = Stage.AtDoor;
            }

            if (state.Stage == Stage.Away)
                return;

            if (!Alive(state.Donnaway))
            {
                var spot = state.Stage switch
                {
                    Stage.AtTeleporter => T.DonnawayBeamsIn,
                    Stage.AtDoor or Stage.Escorting => T.RendezvousAt,
                    _ => T.DonnawayAtWork
                };

                state.Donnaway = Spawn(map, T.Donnaway, spot, 0f, state.Stage >= Stage.Working ? T.Wormhole : T.WormholeMark, immune: true);
                state.DonnawayLead = 0;

                if (state.Stage == Stage.Escorting)
                    state.Stage = Stage.AtDoor;
            }

            var donnaway = state.Donnaway;

            if (donnaway == null || state.Stage is not (Stage.AtDoor or Stage.Escorting))
                return;

            if (Vector3.Distance(donnaway.Position, T.WormholeMark) <= DeliverRadius)
            {
                state.Stage = Stage.Working;
                state.DonnawayLead = 0;
                Stand(map, donnaway, Ground(map, T.DonnawayAtWork), T.Wormhole);
                StartUpload(map, state, members, now);
                return;
            }

            var lead = members.FirstOrDefault(client => client.Player.EntityId == state.DonnawayLead && Escorting(client.Player));

            if (lead == null)
            {
                lead = members.Where(client => Escorting(client.Player) &&
                        Vector3.Distance(client.Player.Position, donnaway.Position) <= PickupRadius)
                    .OrderBy(client => Vector3.Distance(client.Player.Position, donnaway.Position)).FirstOrDefault();

                if (lead == null)
                {
                    if (state.DonnawayLead != 0)
                        Stand(map, donnaway, donnaway.Position, T.WormholeMark);

                    state.DonnawayLead = 0;
                    state.Stage = Stage.AtDoor;
                    return;
                }

                state.DonnawayLead = lead.Player.EntityId;
                state.Stage = Stage.Escorting;
                donnaway.Controller.ScriptedMove = null;
                BehaviorManager.Instance.SetActionFollow(donnaway, lead.Player.EntityId);
                CreatureManager.PublishEscortStatus(map, donnaway, true);
                Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: Donnaway follows {lead.Player.FamilyName}.");
            }

            var to = lead.Player.Position;

            if (Vector3.Distance(donnaway.Position, to) <= WarpDistance && map.NavMesh?.Connected(donnaway.Position, to) != false)
                return;

            // Too far, or where she cannot walk: put beside them.
            var beside = NavMeshManager.RandomPointAround(map, to, 3f) is { } near && Vector3.Distance(near, to) <= 5f ? near : to;
            beside = Ground(map, beside);

            if (BehaviorManager.Instance.RestoreScriptedPose(map, donnaway, beside, Math.Atan2(to.X - beside.X, to.Z - beside.Z)))
            {
                donnaway.Controller.ScriptedMove = null;
                BehaviorManager.Instance.SetActionFollow(donnaway, lead.Player.EntityId);
            }
        }

        private static void EnsureDonnaway(MapChannel map, State state, Vector3 spot, Vector3 facing)
        {
            if (!Alive(state.Donnaway))
            {
                state.Donnaway = Spawn(map, T.Donnaway, spot, 0f, facing, immune: true);
                return;
            }

            Stand(map, state.Donnaway, Ground(map, spot), facing);
        }

        /// <summary>Donnaway on the mark: Ryxnl through the main door with soldiers, and Hunters on the balconies.</summary>
        private static void StartUpload(MapChannel map, State state, List<Client> members, long now)
        {
            state.UploadFrom = now;
            SendWave(map, state, 0, members);

            foreach (var member in members)
                CommunicatorManager.Instance.SystemMessage(member, "Donnaway starts the upload. Bane coming through the main door!");

            Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: Donnaway is at the wormhole; the upload begins.");
        }

        /// <summary>The waves, the upload's end and the hologram.</summary>
        private static void Upload(MapChannel map, State state, List<Client> members, long now)
        {
            if (state.Stage < Stage.Working)
                return;

            for (var i = 0; i < state.Waves.Length; i++)
                state.Waves[i].RemoveAll(creature => !Alive(creature));

            if (!state.UploadDone)
            {
                if (!state.WaveSent[0])
                    SendWave(map, state, 0, members);

                if (!state.WaveSent[1] && now - state.UploadFrom >= Wave2Ms)
                    SendWave(map, state, 1, members);

                if (!state.WaveSent[2] && now - state.UploadFrom >= Wave3Ms)
                    SendWave(map, state, 2, members);

                if (now - state.UploadFrom >= UploadMs && state.WaveSent.All(sent => sent) && state.Waves.All(wave => wave.Count == 0))
                {
                    state.UploadDone = true;
                    state.Stage = Stage.Done;

                    foreach (var member in members)
                        CommunicatorManager.Instance.SystemMessage(member, "Upload complete. The wormhole shudders and goes dark... and something flickers into view beside it.");

                    Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: the upload is done.");
                }
            }

            if ((state.UploadDone || members.Any(client => Done(client.Player, T.WhatGoesAround, T.Protect))) && !Alive(state.Hologram))
                state.Hologram = Spawn(map, T.Hologram, T.HologramAt, 0f, T.Wormhole, immune: true);
        }

        /// <summary>A wave: who, and from where.</summary>
        private static readonly (uint Creature, bool Balcony)[][] Waves =
        {
            new[] { (T.Ryxnl, false), (T.Soldier, false), (T.Soldier, false), (T.Hunter, true), (T.Hunter, true) },
            new[] { (T.Soldier, false), (T.Soldier, false), (T.Soldier, false), (T.Caretaker, false) },
            new[] { (T.Grenadier, false), (T.Grenadier, false), (T.Soldier, false), (T.Soldier, false), (T.Lightbender, true) }
        };

        private static void SendWave(MapChannel map, State state, int wave, List<Client> members)
        {
            state.WaveSent[wave] = true;
            var balcony = 0;

            foreach (var (row, onBalcony) in Waves[wave])
            {
                var spot = onBalcony ? T.Balconies[balcony++ % T.Balconies.Length] : T.MainDoor;

                if (Spawn(map, row, spot, 3f, T.DonnawayAtWork) is not { } bane)
                    continue;

                if (members.Count > 0)
                    bane.Hate.Ensure(Nearest(members, T.DonnawayAtWork).Player.EntityId, Threat.NoticedThreat);

                state.Waves[wave].Add(bane);
            }

            Logger.WriteLog(LogType.Debug, $"Tahrendra Base {map.InstanceId}: wave {wave + 1}, {state.Waves[wave].Count} Bane.");
        }

        /// <summary>The copy's waves, for a test: the living Bane of each.</summary>
        public static IReadOnlyList<IReadOnlyList<Creature>> WavesOf(MapChannel map) =>
            StateOf(map)?.Waves.Select(wave => (IReadOnlyList<Creature>)wave.Where(Alive).ToList()).ToList() ??
            new List<IReadOnlyList<Creature>>();

        public static bool UploadDone(MapChannel map) => StateOf(map)?.UploadDone == true;

        public static Creature HologramOf(MapChannel map) => StateOf(map)?.Hologram is { } hologram && Alive(hologram) ? hologram : null;

        private static bool WaveDown(State state, int wave) => state.WaveSent[wave] && state.Waves[wave].Count == 0;

        #endregion

        #region The missions

        /// <summary>What the copy has done, to the member's missions that wait on it.</summary>
        private static void Tell(State state, Client client, long now)
        {
            var player = client.Player;

            if (state.Sealed.All(done => done) && Wants(player, T.BreakOnThrough, T.SealBarracks))
                Record(state, client, T.BreakOnThrough, T.BarracksSealed, now);

            if (state.TeleporterOn && Wants(player, T.BringingTheHouseDown, T.Teleporter))
                Record(state, client, T.BringingTheHouseDown, T.TeleporterOn, now);

            if ((state.BioBombSet || state.BioFuseAt != 0) && Wants(player, T.BringingTheHouseDown, T.BioBomb))
                Record(state, client, T.BringingTheHouseDown, T.BioBombSet, now);

            if (state.Turned.TryGetValue(player.Id, out var turned) && turned.Count >= T.RobotsWanted &&
                Wants(player, T.BringingTheHouseDown, T.Robots))
                Record(state, client, T.BringingTheHouseDown, T.RobotsGathered, now);

            if (state.UpperBlockSecured && Wants(player, T.BringingTheHouseDown, T.SecureStation3))
                Record(state, client, T.BringingTheHouseDown, T.UpperBlockSecured, now);

            if (state.Stage >= Stage.Working && Wants(player, T.WhatGoesAround, T.EscortDonnaway))
                Record(state, client, T.WhatGoesAround, T.DonnawayDelivered, now);

            if (WaveDown(state, 1) && Wants(player, T.WhatGoesAround, T.Wave2))
                Record(state, client, T.WhatGoesAround, T.Wave2Down, now);

            if (WaveDown(state, 2) && Wants(player, T.WhatGoesAround, T.Wave3))
                Record(state, client, T.WhatGoesAround, T.Wave3Down, now);

            if (state.UploadDone && Wants(player, T.WhatGoesAround, T.Protect))
                Record(state, client, T.WhatGoesAround, T.UploadDone, now);
        }

        #endregion

        #region Helpers

        /// <summary>The base's people take no harm, each one as it comes into the world again.</summary>
        private static void ProtectAfs(MapChannel map)
        {
            foreach (var creature in Creatures(map))
                if (!creature.ImmuneToAllDamage && Array.IndexOf(T.Afs, creature.DbId) >= 0)
                    creature.ImmuneToAllDamage = true;
        }

        /// <summary>A creature stands where it is put, facing the spot: nobody's to follow.</summary>
        private static void Stand(MapChannel map, Creature creature, Vector3 at, Vector3 facingTo)
        {
            creature.Controller.ActionFollow.FollowTargetId = 0;
            CreatureManager.PublishEscortStatus(map, creature, false);

            if (BehaviorManager.Instance.RestoreScriptedPose(map, creature, at, Math.Atan2(facingTo.X - at.X, facingTo.Z - at.Z)))
            {
                creature.Controller.ScriptedMove = null;
                creature.Controller.CurrentAction = BehaviorManager.BehaviorActionIdle;
            }
        }

        /// <summary>The living players in the copy.</summary>
        private static List<Client> Members(MapChannel map) =>
            map.ClientList?.Where(client => client?.Player != null && client.State == ClientState.Ingame &&
                    client.Player.MapChannel == map && client.Player.State != CharacterState.Dead &&
                    client.Player.State != CharacterState.Dying).ToList()
            ?? new List<Client>();

        private static Client Nearest(List<Client> clients, Vector3 to) =>
            clients.OrderBy(client => Vector3.Distance(client.Player.Position, to)).First();

        private static IEnumerable<Creature> Creatures(MapChannel map) =>
            map.MapCellInfo.Cells.Values.SelectMany(cell => cell.CreatureList).Distinct().Where(Alive).ToList();

        private static bool Active(Manifestation player, uint missionId) =>
            player.Missions.TryGetValue(missionId, out var log) && log.State == MissionState.Active;

        /// <summary>Whether the player has the mission done: in their history, or succeeded and not yet handed in.</summary>
        private static bool Done(Manifestation player, uint missionId) =>
            player.MissionHistory.TryGetValue(missionId, out var state) && state is MissionState.Completed or MissionState.Success ||
            player.Missions.TryGetValue(missionId, out var log) && log.State is MissionState.Completed or MissionState.Success;

        /// <summary>Whether the player has the mission with the objective done.</summary>
        private static bool Done(Manifestation player, uint missionId, uint objectiveId) =>
            player.Missions.TryGetValue(missionId, out var log) &&
            log.Objectives.TryGetValue(objectiveId, out var objective) && objective.State == MissionObjectiveState.Completed;

        /// <summary>Whether the player has the mission with the objective open.</summary>
        public static bool Wants(Manifestation player, uint missionId, uint objectiveId) =>
            player.Missions.TryGetValue(missionId, out var log) && log.State == MissionState.Active &&
            log.Objectives.TryGetValue(objectiveId, out var objective) && objective.State == MissionObjectiveState.Incomplete;

        /// <summary>The event to the player's mission: once for an assignment, or again a while after one that changed nothing.</summary>
        private static void Record(State state, Client client, uint missionId, uint eventId, long now)
        {
            if (!client.Player.Missions.TryGetValue(missionId, out var log))
                return;

            var key = $"{client.Player.Id}:{log.AssignmentId}:{missionId}:{eventId}";

            if (state.Given.TryGetValue(key, out var retryAt) && now < retryAt)
                return;

            state.Given[key] = MissionApplication.Instance.TryEmitScenarioProgressEvent(client, missionId, T.Scenario, eventId)
                ? long.MaxValue
                : now + RetryMs;
        }

        /// <summary>The navmesh under a spot, if it is near enough; the spot as it is if not.</summary>
        private static Vector3 Ground(MapChannel map, Vector3 spot) => NavMeshManager.SnapToGround(map, spot);

        /// <summary>A creature of the row, put on the ground near the spot, facing the other.</summary>
        private static Creature Spawn(MapChannel map, uint dbId, Vector3 spot, float spread, Vector3 facingTo, bool immune = false)
        {
            var creature = CreatureManager.Instance.CreateCreature(dbId, null);

            if (creature == null)
                return null;

            var at = spot;

            if (spread > 0 && NavMeshManager.RandomPointAround(map, spot, spread) is { } walkable &&
                Vector2.Distance(new Vector2(walkable.X, walkable.Z), new Vector2(spot.X, spot.Z)) <= spread + 1f)
                at = walkable;

            at = Ground(map, at);

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

        /// <summary>Forgets every copy; for tests.</summary>
        public static void Reset() => States.Clear();

        #endregion
    }
}
