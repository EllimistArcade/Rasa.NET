using System;
using System.Collections.Generic;

namespace Rasa.Structures
{
    using Data;

    /// <summary>
    /// A buff, debuff or looping gesture on an actor. TypeId is the client's gameeffectdata id
    /// (which picks the client-side effect class and its visuals), EffectId identifies this
    /// instance to the client the way an entity id identifies an entity, and EffectLevel is the
    /// pump level it was applied at. Timing is in Environment.TickCount64 so it does not depend
    /// on how often the effect worker happens to run.
    ///
    /// What an effect does is the sum of the fields below that are set. While it is on, the
    /// modifiers (damage dealt, resistance, regeneration, maximum health, movement) are read by
    /// whoever computes the thing they modify. On each tick it may drain adrenaline, damage or
    /// heal its holder, damage everything hostile around its holder, or, as an aura, keep a copy
    /// of itself on the squad within reach. GameEffectManager runs all of it.
    /// </summary>
    public class GameEffect
    {
        public int TypeId { get; set; }
        public int EffectId { get; set; }
        public uint EffectLevel { get; set; }

        /// <summary>The action that attached it, if any; what is looked up for its numbers.</summary>
        public ActionId ActionId { get; set; }

        /// <summary>Who attached it; the actor itself for self buffs.</summary>
        public ulong SourceId { get; set; }

        /// <summary>
        /// The actor behind SourceId, for ticks that deal damage: they are credited with a kill
        /// and are what a surviving creature turns on. May have left the map by the time a tick
        /// runs; the manager checks.
        /// </summary>
        public Actor Source { get; set; }

        /// <summary>The source's level when the effect was attached; tick amounts scale to it.</summary>
        public int SourceLevel { get; set; } = 1;

        /// <summary>The actor it is on. Set by Attach.</summary>
        public Actor Holder { get; set; }

        /// <summary>Environment.TickCount64 at which it wears off; long.MaxValue for "until detached".</summary>
        public long ExpiresTick { get; set; } = long.MaxValue;

        /// <summary>Milliseconds between ticks, for effects that do something on a schedule; 0 for none.</summary>
        public int TickIntervalMs { get; set; }

        /// <summary>Environment.TickCount64 of the next tick.</summary>
        public long NextTickTick { get; set; }

        /// <summary>Buff or debuff, for the client's icon colouring and right-click rules.</summary>
        public bool IsBuff { get; set; } = true;

        /// <summary>
        /// Whether the attach packet itself announces the effect (plays its attach FX and posts
        /// the status icon). An ability's effects are attached quietly and announced by the
        /// ability's own recovery on the client - PerformRecovery names the entities hit and the
        /// client calls AnnounceGameEffectAttach on each - so the visuals land with the action.
        /// Anything not announced by an action says so here.
        /// </summary>
        public bool AnnounceOnAttach { get; set; } = true;

        /// <summary>
        /// The client may ask for this effect to be removed (RequestDetachGameEffect) - a toggle
        /// like sprint, or any buff the player right-clicks away. Debuffs stay.
        /// </summary>
        public bool AllowDetach { get; set; }

        /// <summary>
        /// A weapon skill's standing effect (ManifestationManager.SyncWeaponSkills): there for as
        /// long as the player has the skill, one per skill rather than one per type, and seen by
        /// the player's own client alone - it is how that client learns what its heat meter and
        /// reload bar should do, and nobody else's client has any use for it.
        /// </summary>
        public bool IsSkillPassive { get; set; }

        /// <summary>
        /// Values for the client's tooltip beyond the fixed ones (duration, damage type, buff
        /// flags), keyed as the effect's tooltip format string names them - dmgMod, resistMod,
        /// healMin and so on. Every key the string uses has to be here or the client's %
        /// formatting throws.
        /// </summary>
        public Dictionary<string, object> Tooltip { get; } = new Dictionary<string, object>();

        #region Modifiers while on

        /// <summary>Movement speed as a percent of normal while the effect is on: 120 is a fifth faster. 0 means no change.</summary>
        public int MovementModifierPercent { get; set; }

        /// <summary>Percent added to the damage the holder deals with weapons and abilities; negative reduces it.</summary>
        public int DamageDealtPercent { get; set; }

        /// <summary>Added to the holder's resistance to everything; see GameEffectManager.ResistMultiplier.</summary>
        public int ResistModifier { get; set; }

        /// <summary>Percent added to the holder's health and power regeneration: 400 is five times the rate.</summary>
        public int RegenPercent { get; set; }

        /// <summary>Percent added to the holder's armour regeneration.</summary>
        public int ArmorRegenPercent { get; set; }

        /// <summary>Percent change to the holder's maximum health while on; negative lowers it.</summary>
        public int MaxHealthPercent { get; set; }

        /// <summary>The points of maximum health actually added (or taken) when it was attached, so exactly that is put back.</summary>
        public int MaxHealthApplied { get; set; }

        #endregion

        #region Ticks

        /// <summary>Percent of the actor's maximum chi (adrenaline) taken per second while the effect is on; the effect ends when the bar is empty.</summary>
        public double AdrenalineDrainPercentPerSecond { get; set; }

        /// <summary>The fraction of a point of drain carried to the next tick, so 1.5 a second takes 3 every two seconds and not 2.</summary>
        public double DrainCarry { get; set; }

        /// <summary>Damage rolled on each tick, before level scaling; 0 for no damage tick.</summary>
        public int TickDamageMin { get; set; }
        public int TickDamageMax { get; set; }
        public DamageType TickDamageType { get; set; } = DamageType.Physical;

        /// <summary>DAMAGE_SCALE_TYPE for the tick's damage and healing; see AbilityManager.Scale.</summary>
        public int TickScaleType { get; set; }

        /// <summary>Metres around the holder the tick's damage reaches; 0 means the holder is what is damaged.</summary>
        public float TickRadius { get; set; }

        /// <summary>Healing on each tick, before level scaling; 0 for none.</summary>
        public int TickHealMin { get; set; }
        public int TickHealMax { get; set; }

        /// <summary>Adrenaline given to the holder on each tick; 0 for none.</summary>
        public int TickAdrenaline { get; set; }

        #endregion

        #region Aura

        /// <summary>
        /// Metres around the holder within which squad members carry a copy of this effect. The
        /// copies (Children) come and go with the squad as it moves, on the effect's tick, and go
        /// with the effect when it ends.
        /// </summary>
        public float AuraRadius { get; set; }

        /// <summary>The gameeffectdata id the copies use; the client often has a separate class for the aura's recipients.</summary>
        public int AuraChildTypeId { get; set; }

        /// <summary>
        /// Whether a new copy is announced through this effect's tick (GameEffectTick with the
        /// recipients' ids, as RageSourceEffect.OnTick expects) rather than by its own attach.
        /// </summary>
        public bool AuraTickAnnounces { get; set; }

        /// <summary>The aura this is a copy of, or null.</summary>
        public GameEffect Parent { get; set; }

        /// <summary>The copies this aura has out.</summary>
        public List<GameEffect> Children { get; } = new List<GameEffect>();

        #endregion

        public bool IsExpired => Environment.TickCount64 >= ExpiresTick;

        public bool TickDue => TickIntervalMs > 0 && Environment.TickCount64 >= NextTickTick;

        public bool HasDuration => ExpiresTick != long.MaxValue;

        /// <summary>Whole seconds left, for the client's tooltip; 0 for an effect with no end.</summary>
        public int RemainingSeconds => HasDuration ? (int)Math.Max(0, (ExpiresTick - Environment.TickCount64) / 1000) : 0;

        /// <summary>Whether a tick of this effect does anything besides announce itself.</summary>
        public bool TicksDoWork => AdrenalineDrainPercentPerSecond > 0 || TickDamageMax > 0 || TickHealMax > 0 || TickAdrenaline > 0 || AuraRadius > 0;
    }
}
