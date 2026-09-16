using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rasa.Structures
{
    using Char;
    using Data;
    using Repositories.Char.Character;

    public class Manifestation : Actor, ICharacterChange
    {
        public uint Id { get; set; }
        public uint Gender { get; set; }
        public Dictionary<EquipmentData, AppearanceData> AppearanceData { get; set; }
        public List<CharacterOptions> CharacterOptions = new();
        public double Scale { get; set; }
        public Race Race { get; set; }
        public uint Class { get; set; }
        public uint Experience { get; set; }
        public byte Level { get; set; }
        public uint CloneCredits { get; set; }
        public uint NumLogins { get; set; }
        public uint TotalTimePlayed { get; set; }
        public DateTime? TimeSinceLastPlayed { get; set; }
        public uint ClanId { get; set; }
        public string ClanName { get; set; }
        public int LockboxCredits { get; set; }
        public int LockboxTabs { get; set; }
        public Dictionary<CurencyType, int> Credits = new();

        public List<ResistanceData> ResistanceData = new();
        public int SpentBody { get; set; }
        public int SpentMind { get; set; }
        public int SpentSpirit { get; set; }
        public Dictionary<SkillId, SkillsData> Skills = new();
        public Dictionary<int, AbilityDrawerData> Abilities = new();
        public List<uint> Titles { get; set; } = new List<uint>();
        public uint CurrentTitle { get; set; }
        public int CurrentAbilityDrawer { get; set; }
        public Dictionary<int, MissionLog> Missions { get; set; } = new();
        public DateTime LoginTime { get; set; }
        public List<uint> Logos = new();
        public ulong TrackingTargetEntityId { get; set; }
        public byte ActiveWeapon { get; set; }
        public List<CharacterTeleporterEntry> GainedWaypoints = new();
        public bool IsAFK { get; set; }

        /// <summary>
        /// The best quality the client will pick up by walking over a corpse. Set by
        /// SetAutoLootThreshold, which the client sends at login and whenever the option changes.
        /// Junk is the client's own default (gameui: GetOptionString(..., 'Junk')), so an account
        /// that has never touched the option auto-loots junk and nothing else.
        /// </summary>
        public LootQuality AutoLootThreshold { get; set; } = LootQuality.Junk;

        /// <summary>
        /// Environment.TickCount64 at the player's last movement or action. Monotonic, so a
        /// wall-clock change on the server cannot make everyone idle at once.
        /// </summary>
        public long LastActivityTick { get; set; } = Environment.TickCount64;

        /// <summary>Whether PlayerInactiveWarning has gone out for the current idle stretch.</summary>
        public bool InactiveWarningSent { get; set; }

        /// <summary>
        /// Always false: this server has no trial accounts. The single source for every packet
        /// that reports the flag (IsTrialAccount, WhoAck), so the client never shows the trial
        /// tag and no trial-only restriction - whisper, party or clan invites, trial chat
        /// channels - ever applies.
        /// </summary>
        public bool IsTrialAccount => false;

        // Inventory
        public Inventory Inventory { get; set; } = new Inventory();

        // Party
        internal uint PartyId { get; set; }
        /// <summary>AcceptPartyInvitesChanged; invitations to a player who turned them off are refused.</summary>
        internal bool AcceptPartyInvites { get; set; } = true;

        // Social
        internal List<uint> Friends = new();
        internal List<uint> IgnoredPlayers = new();
        public MapChannel MapChannel { get; set; }
        public bool Disconected { get; set; }
        /// <summary>Set by RequestLogout, cleared by CancelLogoutRequest.</summary>
        public bool LogoutActive { get; set; }

        /// <summary>
        /// Whether this player is in a fight, as the server counts it: they have dealt or taken
        /// damage within the last <see cref="Data.CombatRegen.CombatTimeoutMs"/>.
        ///
        /// Distinct from <c>InCombatMode</c> on Actor, which is the visual weapon stance the
        /// client asks for with RequestVisualCombatMode and which says nothing about whether
        /// anyone is actually fighting.
        /// </summary>
        public bool InCombat { get; set; }

        /// <summary>Environment.TickCount64 at which combat lapses, refreshed by every hit.</summary>
        public long CombatExpiresAt { get; set; }

        /// <summary>Environment.TickCount64 when the pending logout was requested.</summary>
        public long LogoutRequestedTick { get; set; }
        public bool RemoveFromMap { get; set; }

        /// <summary>
        /// Ids of the map links whose trigger radius the player is standing in. A link fires
        /// when its id joins this set, so a player who arrives inside the reciprocal gate is not
        /// bounced straight back: MapLinkManager seeds it on arrival and clears it on leaving.
        /// </summary>
        internal HashSet<uint> InsideMapLinks = new();

        /// <summary>
        /// The region ids the client was last told the player is in (UpdateRegions), sorted, so
        /// RegionManager only sends again when the set changes. Null until the first send on a map.
        /// </summary>
        internal List<uint> RegionIds;

        /// <summary>Set by .setregion: the list above was forced and the worker leaves it alone until released or the map changes.</summary>
        internal bool RegionsHeld;
        // chat
        public int JoinedChannels { get; set; }
        public int[] ChannelHashes = new int[14];
        // gm flags
        public bool GmFlagAlwaysFriendly { get; set; }


        public Manifestation()
        {
        }

        public Manifestation(CharacterEntry character, Dictionary<EquipmentData, AppearanceData> appearence)
        {
            // CharacterData
            Id = character.Id;
            Scale = character.Scale;
            Race = (Race)character.Race;
            Class = character.Class;
            Experience = character.Experience;
            Level = character.Level;
            SpentBody = character.Body;
            SpentMind = character.Mind;
            SpentSpirit = character.Spirit;
            CloneCredits = character.CloneCredits;
            Credits.Add(CurencyType.Credits, character.Credit);
            Credits.Add(CurencyType.Prestige, character.Prestige);
            ActiveWeapon = character.ActiveWeapon;
            NumLogins = character.NumLogins + 1;
            TotalTimePlayed = character.TotalTimePlayed;
            TimeSinceLastPlayed = character.LastLogin;
            // AppearanceData
            AppearanceData = appearence;
            // Actor
            EntityClass = character.Gender == 0 ? EntityClasses.HumanBaseMale : EntityClasses.HumanBaseFemale;
            Name = character.Name;
            FamilyName = character.GameAccount.FamilyName;
            Position = new Vector3((float)character.CoordX, (float)character.CoordY, (float)character.CoordZ);
            Rotation = (float)character.Rotation;
            MapContextId = character.MapContextId;
            IsRunning = character.IsRunning();
            InCombatMode = false;
            State = CharacterState.Normal;
            MovementSpeed = 1.0d;
            Attributes = new Dictionary<Attributes, ActorAttributes>() {
                        { Data.Attributes.Body, new ActorAttributes(Data.Attributes.Body, 0, 0, 0, 0, 0) },
                        { Data.Attributes.Mind, new ActorAttributes(Data.Attributes.Mind, 0, 0, 0, 0, 0) },
                        { Data.Attributes.Spirit, new ActorAttributes(Data.Attributes.Spirit, 0, 0, 0, 0, 0) },
                        { Data.Attributes.Health, new ActorAttributes(Data.Attributes.Health, 0, 0, 0, 0, 0) },
                        { Data.Attributes.Chi, new ActorAttributes(Data.Attributes.Chi, 0, 0, 0, 0, 0) },
                        { Data.Attributes.Power, new ActorAttributes(Data.Attributes.Power, 0, 0, 0, 0, 0) },
                        { Data.Attributes.Aware, new ActorAttributes(Data.Attributes.Aware, 0, 0, 0, 0, 0) },
                        { Data.Attributes.Armor, new ActorAttributes(Data.Attributes.Armor, 0, 0, 0, 0, 0) },
                        { Data.Attributes.Speed, new ActorAttributes(Data.Attributes.Speed, 0, 0, 0, 0, 0) },
                        { Data.Attributes.Regen, new ActorAttributes(Data.Attributes.Regen, 0, 0, 0, 0, 0) }
                };
        }
    }
}
