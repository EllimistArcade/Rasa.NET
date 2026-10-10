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
    ///
    /// The Brann dissection tables and the Bane stasis chambers. Both are inert destroyables
    /// (augmentation 41) that the .maps place, and both have their idle animation and effect in
    /// USE_IDES_STATE_INTACT (110), so both have stood still. The tables
    /// (UsableInertDestBrannTableDissectionAttaV01DELETEDUPE, 25269 - the maps use the
    /// DELETEDUPE class; 24607, the same table, is on none): "Usable Inactive", animation family
    /// 277 as recurring one-shots (type 3), the Atta on the table between
    /// prop_brann_table_dissection_atta_v01.anm and _v02.anm, with
    /// prop_brann_table_dissection_atta.pkg, the table's lasers (vfx_prop_brann_table_atta_laser)
    /// under its grate lights. The chambers (UsableInertDestBaneStasisChamber, 7197): the same
    /// spec, looping, arch_bane_industrial_room_stasis_chamber.anm, with
    /// arch_bane_Industrial_stasis_chamber_idle.pkg (arch_bane_industrial_room_stasis_chamber_fx).
    /// Neither is used: an inert destroyable offers only a loot use (Lootable), and these have
    /// none. Neither is destroyed here either: the client takes either for undamageable
    /// (usabledata.lookup has no hit points for them), and the explosion and wreck their
    /// destroyed states carry are left for when something destroys them.
    ///
    /// The Brann monitors. UsableTwoStateBrannMonitorGenericV01 (23885) and V02 (23886) are
    /// two-state switches like the fire pits, the other way about: USE_TS_STATE_1 (56) is the
    /// one with an animation (usabledata.animation: Usable state 1 static, played looping,
    /// arch_brann_gen_obj_monitor_v01_on.anm on both meshes), and USE_TS_STATE_0 (55) has
    /// nothing, a dark screen. Neither has an effect, a transition animation or an alternate
    /// mesh. They are sent 56, on. Their classes are not targetable (entityclass target flag 0),
    /// so the client offers no Use and they are not used. Sixty-eight on the maps: thirty-one
    /// V01 and thirty-three V02 in the Comm Tower, four V01 in Burning Steps. The Brann monitor
    /// props the .maps also place (PropBrannMonitorV04 and the destroyed ones) are no usables
    /// and have no states.
    ///
    /// The Bane articulated drills. UsableInertDestBaneArticulatedDrillV01 (6318) is an inert
    /// destroyable like the tables and chambers, and works in USE_IDES_STATE_INTACT (110):
    /// "Usable Inactive", animation family 277 as recurring one-shots (type 3), the drill going
    /// between arch_bane_gen_obj_articulateddrill_drilling_v01.anm, _idle_fidget_v01.anm and
    /// _idle_long_v01.anm, with arch_bane_gen_obj_articulateddrill_v01.pkg. Its damaged states
    /// (50% and 25%: the drilling alone) and its destroyed one (wreck, explosion) are not used,
    /// as the client has no hit points for it. Three on the maps: Concordia Divide, by the Timora
    /// Mines tunnel; the Treeback Camp; Thunderhead.
    ///
    /// Not here: the sonic towers of Maligo Base (UsableInertDestBaneSonicTowerV02, 21964, two
    /// on adv_foreas_valverde_plateau_maligobasev3). Their class has nothing in any intact
    /// state - no animation, no effect - only its destruction (the 25% to destroyed transition,
    /// arch_bane_gen_obj_sonictower_v02_exploding.anm and _exploding.pkg, then the wreck and
    /// _destroyed.pkg), and it is neither targetable nor given hit points, so there is no state
    /// to send that would show anything.
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
        public const uint PravusResearch = 1430;        // adv_foreas_concordia_wilderness_pravusresearch
        public const uint TestWeaponsCenter = 1465;     // adv_foreas_valverde_pools_test_weapons_center
        public const uint PenalResearch = 2034;         // adv_arieki_torden_plains_penalresearch
        public const uint StaalJunkyard = 2138;         // adv_arieki_ligo_staaljunkyard
        public const uint BurningSteps = 1993;          // adv_arieki_ligo_burningsteps
        public const uint CommTower = 2085;             // adv_arieki_torden_incline_commtower
        public const uint TreebackCamp = 1397;          // adv_foreas_concordia_palisades_treebackcamp
        public const uint Thunderhead = 1911;           // adv_arieki_ligo_thunderhead

        public const uint ForeanFirePitV01 = 6137;     // UsableTwoStateForeanFirePitV01
        public const uint ForeanFirePitV03 = 6212;     // UsableTwoStateForeanFirePitV03
        public const uint BaneStasisChamber = 7197;    // UsableInertDestBaneStasisChamber
        public const uint BrannDissectionTable = 25269; // UsableInertDestBrannTableDissectionAttaV01DELETEDUPE
        public const uint BrannMonitorV01 = 23885;     // UsableTwoStateBrannMonitorGenericV01
        public const uint BrannMonitorV02 = 23886;     // UsableTwoStateBrannMonitorGenericV02
        public const uint BaneArticulatedDrill = 6318; // UsableInertDestBaneArticulatedDrillV01

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

            // The Brann dissection tables: one in Staal Junkyard, eighteen in Penal Research.
            DissectionTable(StaalJunkyard, 134419591465348, -96.6515f, 253.7253f, -102.9956f, "Staal Junkyard dissection table"),
            DissectionTable(PenalResearch, 134419591466928, -8.5f, -31.44f, -44.75f, "Penal Research dissection table 1"),
            DissectionTable(PenalResearch, 134419591466950, -103.2895f, -47.4f, -61.5658f, "Penal Research dissection table 2"),
            DissectionTable(PenalResearch, 134419591466953, -104.04f, -47.4f, -82.2f, "Penal Research dissection table 3"),
            DissectionTable(PenalResearch, 134419591467561, -2.25f, -31.44f, -45.0f, "Penal Research dissection table 4"),
            DissectionTable(PenalResearch, 134419591467562, -2.0f, -31.44f, -40.0f, "Penal Research dissection table 5"),
            DissectionTable(PenalResearch, 134419591467563, -8.25f, -31.44f, -39.75f, "Penal Research dissection table 6"),
            DissectionTable(PenalResearch, 134419591467566, -2.0f, -31.44f, -66.25f, "Penal Research dissection table 7"),
            DissectionTable(PenalResearch, 134419591467567, -8.25f, -31.44f, -66.0f, "Penal Research dissection table 8"),
            DissectionTable(PenalResearch, 134419591467568, -2.25f, -31.44f, -71.25f, "Penal Research dissection table 9"),
            DissectionTable(PenalResearch, 134419591467569, -8.5f, -31.44f, -71.0f, "Penal Research dissection table 10"),
            DissectionTable(PenalResearch, 134419591467570, -23.5f, -31.44f, -66.25f, "Penal Research dissection table 11"),
            DissectionTable(PenalResearch, 134419591467571, -29.75f, -31.44f, -66.0f, "Penal Research dissection table 12"),
            DissectionTable(PenalResearch, 134419591467572, -23.75f, -31.44f, -71.25f, "Penal Research dissection table 13"),
            DissectionTable(PenalResearch, 134419591467573, -30.0f, -31.44f, -71.0f, "Penal Research dissection table 14"),
            DissectionTable(PenalResearch, 134419591467574, -24.0f, -31.44f, -40.25f, "Penal Research dissection table 15"),
            DissectionTable(PenalResearch, 134419591467575, -30.25f, -31.44f, -40.0f, "Penal Research dissection table 16"),
            DissectionTable(PenalResearch, 134419591467576, -24.25f, -31.44f, -45.25f, "Penal Research dissection table 17"),
            DissectionTable(PenalResearch, 134419591467577, -30.5f, -31.44f, -45.0f, "Penal Research dissection table 18"),

            // The Bane stasis chambers: one each in Pravus Research and the Test Weapons Center.
            StasisChamber(PravusResearch, 133981504835290, 224.0f, 7.6172f, 40.0f, "Pravus Research stasis chamber"),
            StasisChamber(TestWeaponsCenter, 134131828656196, 280.0f, -24.5f, 24.0f, "Test Weapons Center stasis chamber"),

            // The Bane articulated drills: Concordia Divide (by the Timora Mines tunnel), the Treeback Camp, Thunderhead.
            Drill(ConcordiaDivide, 132770324750504, -554.8872f, 183.4147f, -1104.4224f, "Concordia Divide articulated drill"),
            Drill(TreebackCamp, 133835475914614, 248.069f, 145.4277f, -266.646f, "Treeback Camp articulated drill"),
            Drill(Thunderhead, 134419591464915, -238.4222f, 438.671f, 695.0093f, "Thunderhead articulated drill"),

            // The Brann monitors, on: thirty-one V01 and thirty-three V02 in the Comm Tower, four V01 in Burning Steps.
            Monitor(CommTower, 134419591463952, BrannMonitorV01, 5.6896f, 226.5989f, -14.1714f, "Comm Tower monitor 1"),
            Monitor(CommTower, 134419591464163, BrannMonitorV01, -40.1694f, 250.7855f, 100.8428f, "Comm Tower monitor 2"),
            Monitor(CommTower, 134419591464292, BrannMonitorV01, -11.4553f, 250.5626f, 44.0937f, "Comm Tower monitor 3"),
            Monitor(CommTower, 134419591464293, BrannMonitorV01, -11.4554f, 251.6273f, 44.0937f, "Comm Tower monitor 4"),
            Monitor(CommTower, 134419591464342, BrannMonitorV01, -45.8915f, 234.5242f, 131.542f, "Comm Tower monitor 5"),
            Monitor(CommTower, 134419591464429, BrannMonitorV01, -40.5197f, 249.7524f, 100.1137f, "Comm Tower monitor 6"),
            Monitor(CommTower, 134419591464578, BrannMonitorV01, 79.6341f, 242.7855f, 23.4896f, "Comm Tower monitor 7"),
            Monitor(CommTower, 134419591464587, BrannMonitorV01, 36.5315f, 242.4655f, -4.9972f, "Comm Tower monitor 8"),
            Monitor(CommTower, 134419591464710, BrannMonitorV01, -75.9235f, 250.2799f, 11.8509f, "Comm Tower monitor 9"),
            Monitor(CommTower, 134419591464740, BrannMonitorV01, -110.2525f, 251.5214f, -2.9568f, "Comm Tower monitor 10"),
            Monitor(CommTower, 134419591464945, BrannMonitorV01, -73.872f, 274.774f, -68.0116f, "Comm Tower monitor 11"),
            Monitor(CommTower, 134419591464947, BrannMonitorV01, -72.4938f, 275.2623f, -67.9698f, "Comm Tower monitor 12"),
            Monitor(CommTower, 134419591464952, BrannMonitorV01, -103.9827f, 266.4601f, -48.7736f, "Comm Tower monitor 13"),
            Monitor(CommTower, 134419591464953, BrannMonitorV01, -103.9827f, 267.5248f, -48.7736f, "Comm Tower monitor 14"),
            Monitor(CommTower, 134419591465017, BrannMonitorV01, -29.4334f, 227.2181f, 33.789f, "Comm Tower monitor 15"),
            Monitor(CommTower, 134419591465198, BrannMonitorV01, 107.6415f, 250.7032f, -15.3032f, "Comm Tower monitor 16"),
            Monitor(CommTower, 134419591465210, BrannMonitorV01, 102.7083f, 250.7855f, -53.0515f, "Comm Tower monitor 17"),
            Monitor(CommTower, 134419591465213, BrannMonitorV01, 103.0586f, 249.7524f, -52.3223f, "Comm Tower monitor 18"),
            Monitor(CommTower, 134419591465229, BrannMonitorV01, 5.3097f, 226.7032f, 193.3934f, "Comm Tower monitor 19"),
            Monitor(CommTower, 134419591465232, BrannMonitorV01, 6.0403f, 225.7524f, 192.7309f, "Comm Tower monitor 20"),
            Monitor(CommTower, 134419591465244, BrannMonitorV01, -51.0347f, 266.7032f, 136.7289f, "Comm Tower monitor 21"),
            Monitor(CommTower, 134419591465247, BrannMonitorV01, -50.2495f, 265.7524f, 136.1321f, "Comm Tower monitor 22"),
            Monitor(CommTower, 134419591465250, BrannMonitorV01, -107.9876f, 250.7737f, 39.0919f, "Comm Tower monitor 23"),
            Monitor(CommTower, 134419591465255, BrannMonitorV01, -107.3222f, 249.2524f, 38.3375f, "Comm Tower monitor 24"),
            Monitor(CommTower, 134419591465257, BrannMonitorV01, -104.722f, 250.7311f, -15.9945f, "Comm Tower monitor 25"),
            Monitor(CommTower, 134419591465259, BrannMonitorV01, -104.5862f, 250.1606f, -15.5536f, "Comm Tower monitor 26"),
            Monitor(CommTower, 134419591465262, BrannMonitorV01, -109.7274f, 250.4767f, -3.9442f, "Comm Tower monitor 27"),
            Monitor(CommTower, 134419591465347, BrannMonitorV01, -13.9901f, 227.2737f, 158.3583f, "Comm Tower monitor 28"),
            Monitor(CommTower, 134419591465387, BrannMonitorV01, 79.2665f, 241.7524f, 24.2101f, "Comm Tower monitor 29"),
            Monitor(CommTower, 134419591465396, BrannMonitorV01, -53.8683f, 267.4548f, 88.2033f, "Comm Tower monitor 30"),
            Monitor(CommTower, 134419591465397, BrannMonitorV01, -54.9231f, 266.4102f, 87.8315f, "Comm Tower monitor 31"),
            Monitor(CommTower, 134419591463954, BrannMonitorV02, 4.8277f, 226.6189f, -14.624f, "Comm Tower monitor 32"),
            Monitor(CommTower, 134419591464298, BrannMonitorV02, -11.0027f, 250.5826f, 43.2318f, "Comm Tower monitor 33"),
            Monitor(CommTower, 134419591464344, BrannMonitorV02, -45.7713f, 235.5889f, 131.623f, "Comm Tower monitor 34"),
            Monitor(CommTower, 134419591464346, BrannMonitorV02, -46.3538f, 234.5442f, 130.6853f, "Comm Tower monitor 35"),
            Monitor(CommTower, 134419591464427, BrannMonitorV02, -41.16f, 250.7032f, 100.7105f, "Comm Tower monitor 36"),
            Monitor(CommTower, 134419591464428, BrannMonitorV02, -41.3756f, 251.2737f, 101.1183f, "Comm Tower monitor 37"),
            Monitor(CommTower, 134419591464577, BrannMonitorV02, 80.1292f, 242.7032f, 24.3576f, "Comm Tower monitor 38"),
            Monitor(CommTower, 134419591464579, BrannMonitorV02, 80.5843f, 243.2737f, 24.282f, "Comm Tower monitor 39"),
            Monitor(CommTower, 134419591464585, BrannMonitorV02, 36.3865f, 243.5302f, -4.9972f, "Comm Tower monitor 40"),
            Monitor(CommTower, 134419591464586, BrannMonitorV02, 37.3934f, 242.4855f, -4.5446f, "Comm Tower monitor 41"),
            Monitor(CommTower, 134419591464711, BrannMonitorV02, -76.0559f, 250.1976f, 12.8415f, "Comm Tower monitor 42"),
            Monitor(CommTower, 134419591464739, BrannMonitorV02, -110.2525f, 250.4567f, -2.8119f, "Comm Tower monitor 43"),
            Monitor(CommTower, 134419591464946, BrannMonitorV02, -74.445f, 273.741f, -65.8285f, "Comm Tower monitor 44"),
            Monitor(CommTower, 134419591464954, BrannMonitorV02, -103.8021f, 266.2155f, -46.1963f, "Comm Tower monitor 45"),
            Monitor(CommTower, 134419591464992, BrannMonitorV02, -13.2554f, 226.7032f, -22.7853f, "Comm Tower monitor 46"),
            Monitor(CommTower, 134419591465018, BrannMonitorV02, -30.8875f, 226.7298f, 34.2361f, "Comm Tower monitor 47"),
            Monitor(CommTower, 134419591465196, BrannMonitorV02, 108.922f, 250.7855f, -15.1709f, "Comm Tower monitor 48"),
            Monitor(CommTower, 134419591465197, BrannMonitorV02, 108.5716f, 249.7524f, -15.9f, "Comm Tower monitor 49"),
            Monitor(CommTower, 134419591465211, BrannMonitorV02, 103.9145f, 251.2737f, -53.3269f, "Comm Tower monitor 50"),
            Monitor(CommTower, 134419591465212, BrannMonitorV02, 103.6988f, 250.7032f, -52.9191f, "Comm Tower monitor 51"),
            Monitor(CommTower, 134419591465214, BrannMonitorV02, 103.966f, 249.7524f, -52.3223f, "Comm Tower monitor 52"),
            Monitor(CommTower, 134419591465228, BrannMonitorV02, 5.1363f, 225.7524f, 192.8094f, "Comm Tower monitor 53"),
            Monitor(CommTower, 134419591465230, BrannMonitorV02, 6.5968f, 226.7855f, 193.4145f, "Comm Tower monitor 54"),
            Monitor(CommTower, 134419591465231, BrannMonitorV02, 5.2745f, 227.2737f, 193.8058f, "Comm Tower monitor 55"),
            Monitor(CommTower, 134419591465245, BrannMonitorV02, -49.7542f, 266.7854f, 136.8612f, "Comm Tower monitor 56"),
            Monitor(CommTower, 134419591465246, BrannMonitorV02, -51.1054f, 267.2737f, 137.1367f, "Comm Tower monitor 57"),
            Monitor(CommTower, 134419591465248, BrannMonitorV02, -51.157f, 265.7524f, 136.1321f, "Comm Tower monitor 58"),
            Monitor(CommTower, 134419591465251, BrannMonitorV02, -106.7252f, 250.2855f, 39.9409f, "Comm Tower monitor 59"),
            Monitor(CommTower, 134419591465254, BrannMonitorV02, -107.4447f, 250.2032f, 39.0524f, "Comm Tower monitor 60"),
            Monitor(CommTower, 134419591465256, BrannMonitorV02, -76.6909f, 249.2434f, 12.0563f, "Comm Tower monitor 61"),
            Monitor(CommTower, 134419591465258, BrannMonitorV02, -105.5852f, 250.2429f, -14.7417f, "Comm Tower monitor 62"),
            Monitor(CommTower, 134419591465398, BrannMonitorV02, -53.725f, 266.3901f, 88.1817f, "Comm Tower monitor 63"),
            Monitor(CommTower, 134419591465433, BrannMonitorV02, -29.7443f, 226.6476f, 34.2359f, "Comm Tower monitor 64"),
            Monitor(BurningSteps, 134419591467501, BrannMonitorV01, -200.0808f, 199.7323f, 192.3138f, "Burning Steps monitor 1"),
            Monitor(BurningSteps, 134419591467502, BrannMonitorV01, -189.4679f, 199.7323f, 192.284f, "Burning Steps monitor 2"),
            Monitor(BurningSteps, 134419591467503, BrannMonitorV01, -210.58f, 199.7323f, 192.246f, "Burning Steps monitor 3"),
            Monitor(BurningSteps, 134419591467504, BrannMonitorV01, -220.9307f, 199.7323f, 192.2075f, "Burning Steps monitor 4"),
        };

        /// <summary>A Brann dissection table, intact.</summary>
        private static Usable DissectionTable(uint mapContextId, ulong entityId, float x, float y, float z, string name) =>
            new Usable(mapContextId, entityId, BrannDissectionTable, new Vector3(x, y, z), UseObjectState.IdesStateIntact, null, name);

        /// <summary>A Bane stasis chamber, intact.</summary>
        private static Usable StasisChamber(uint mapContextId, ulong entityId, float x, float y, float z, string name) =>
            new Usable(mapContextId, entityId, BaneStasisChamber, new Vector3(x, y, z), UseObjectState.IdesStateIntact, null, name);

        /// <summary>A Bane articulated drill, intact.</summary>
        private static Usable Drill(uint mapContextId, ulong entityId, float x, float y, float z, string name) =>
            new Usable(mapContextId, entityId, BaneArticulatedDrill, new Vector3(x, y, z), UseObjectState.IdesStateIntact, null, name);

        /// <summary>A Brann monitor, on.</summary>
        private static Usable Monitor(uint mapContextId, ulong entityId, uint classId, float x, float y, float z, string name) =>
            new Usable(mapContextId, entityId, classId, new Vector3(x, y, z), UseObjectState.TsState1, null, name);

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
