using System;
using System.Linq;

namespace Rasa.Managers
{
    using Data;
    using Packets.MapChannel.Server;
    using Structures;

    /// <summary>
    /// Creature attacks that put a game effect on the player they hit. The effect is what the
    /// action's client class names as its targetGameEffect (or, for the Lightbender's quill, its
    /// GAME_EFFECT_ID), and its numbers are the action's level's:
    ///
    ///  - Damage over time, no hit of its own (DecayAction, AttaHarvesterAcidSpitAbility - neither
    ///    class has a DoAbility): DECAY 82 / ATTA_HARVESTER_ACID_SPIT 379, a tick every INTERVAL
    ///    seconds for DURATION, the first an interval in. The creature_action row's damage is the
    ///    tick's.
    ///  - Hit and hold (XanxWebAbility, HunterNetAbility): the row's damage as a hit, and XANX_WEB
    ///    301 / HUNTER_NET 258 for DURATION seconds. Both effects' OnAnnounceAttach call
    ///    RequestMovementBlock on the player's own character and OnDetach releases it, so the
    ///    client does the holding.
    ///  - Hit and blind (LightbenderQuillAbility): the row's damage as a hit, and GAME_EFFECT_ID
    ///    388 LIGHTBENDER_QUILL_FLASH for EFFECT_DURATION_MS - a BlindEffect, whose
    ///    OnAnnounceAttach clears the player's target and blocks target lock until it detaches.
    ///  - Resistance down, no hit (AttaHarvesterPheromoneAbility, MiasmaGasCloudAbility):
    ///    ATTA_HARVESTER_PHEROMONE 362, "Physical Resist: -%(debuffAmt)s%%", for
    ///    EFFECT_DURATION_MS; MIASMA_GAS_CLOUD 278, "Resist Ice: -%(debuffAmt)s%%", for DURATION;
    ///    DEBUFF_AMOUNT_MIN..MAX rolled each time. Everyone in the action's area gets it.
    ///  - Polarity Field, no hit: POLARITY_FIELD 262 for DURATION, the argument's DAMAGE_TYPE made
    ///    a vulnerability - the arguments are named for it, _BOSS_SONIC to _BOSS_PHYSICAL. The
    ///    creature's data gives no size; PolarityVulnerability is the player version's
    ///    PER_PUMP_MOD at one pump.
    ///  - Explosive Nanites, no hit: EXPLODING_NANITES_EFFECT 10000020 for DURATION; each time the
    ///    player takes damage, from anything, they explode on them for the row's damage of the
    ///    argument's DAMAGE_TYPE, USE_COUNT times at most, USE_DROPOFF seconds apart
    ///    (AbilityManager.OnPlayerNanites).
    ///
    ///  - Ground blast, no hit (LinkerGroundBlastAbility): a bomb on the player that goes off on
    ///    everyone around them (CreatureBombs.GroundBlast).
    ///
    /// Effects the client class names are attached quietly: the recovery that follows lists the
    /// player as hit, and TargetedAction.OnServerResolution announces the class's
    /// targetGameEffect on every hit - the attach FX, the icon, and for the web and the net the
    /// movement block. The quill's class names none, so its flash is announced as it attaches.
    ///
    /// Only players take these. A creature's attack on another creature - a turret, a pet - is
    /// its hit alone.
    /// </summary>
    public static class CreatureEffectAttacks
    {
        public enum Kind { None, DamageOverTime, Hold, Blind, ResistDown, Polarity, Nanites, GroundBlast }

        public const int DecayTypeId = 82;                      // DECAY
        public const int AcidSpitTypeId = 379;                  // ATTA_HARVESTER_ACID_SPIT
        public const int XanxWebTypeId = 301;                   // XANX_WEB
        public const int HunterNetTypeId = 258;                 // HUNTER_NET
        public const int QuillFlashTypeId = 388;                // LIGHTBENDER_QUILL_FLASH
        public const int PheromoneTypeId = 362;                 // ATTA_HARVESTER_PHEROMONE
        public const int GasCloudTypeId = 278;                  // MIASMA_GAS_CLOUD
        public const int PolarityFieldTypeId = 262;             // POLARITY_FIELD
        public const int ExplodingNanitesTypeId = 10000020;     // EXPLODING_NANITES_EFFECT

        /// <summary>Ours: how far a creature's Polarity Field lowers the one resistance - the player version's PER_PUMP_MOD at one pump.</summary>
        public const int PolarityVulnerability = 10;

        private static readonly Random Random = new Random();

        public static Kind KindOf(string module)
        {
            switch (module)
            {
                case "abilities.decay":
                case "abilities.ai.attaharvesteracidspitability":
                    return Kind.DamageOverTime;
                case "abilities.ai.xanxwebability":
                case "abilities.ai.hunternetability":
                    return Kind.Hold;
                case "abilities.ai.lightbenderquillability":
                    return Kind.Blind;
                case "abilities.ai.attaharvesterpheromoneability":
                case "abilities.ai.miasmagascloudability":
                    return Kind.ResistDown;
                case "abilities.polarityfield":
                    return Kind.Polarity;
                case "abilities.explodingnanites":
                    return Kind.Nanites;
                case "abilities.ai.linkergroundblastability":
                    return Kind.GroundBlast;
                default:
                    return Kind.None;
            }
        }

        public static Kind KindOf(ActionId actionId, uint actionArgId)
        {
            if (AbilityManager.Instance == null || !AbilityManager.Instance.TryGetAction(actionId, actionArgId, out var module, out _))
                return Kind.None;

            return KindOf(module);
        }

        /// <summary>
        /// Whether the attack is a hit as well as its effect. A damage over time's row damage is
        /// its tick's, and the resistance, polarity and nanite attacks do no damage of their own;
        /// their classes have no DoAbility to show one.
        /// </summary>
        public static bool Hits(Kind kind) => kind == Kind.None || kind == Kind.Hold || kind == Kind.Blind;

        /// <summary>A player a creature's attack has hit: the attack's effect, if it carries one.</summary>
        public static void OnHit(MapChannel mapChannel, Creature attacker, Manifestation player, Missile missile)
        {
            if (mapChannel == null || attacker == null || player == null || missile == null || AbilityManager.Instance == null)
                return;

            if (player.State == CharacterState.Dead || player.State == CharacterState.Dying)
                return;

            if (!AbilityManager.Instance.TryGetAction(missile.ActionId, missile.ActionArgId, out var module, out var info) || info == null)
                return;

            var kind = KindOf(module);

            if (kind == Kind.None)
                return;

            // A Linker's ground blast is a bomb on the player, going off on everyone near them.
            if (kind == Kind.GroundBlast)
            {
                CreatureBombs.GroundBlast(mapChannel, attacker, player, missile.CreatureAction, info);
                return;
            }

            var effect = Build(mapChannel, attacker, missile, module, kind, info);

            if (effect != null)
                GameEffectManager.Instance.Attach(mapChannel, player, effect);
        }

        /// <summary>The effect an attack puts on a player, from its module, its level and the creature's row; null for none.</summary>
        public static GameEffect Build(MapChannel mapChannel, Creature attacker, Missile missile, string module, Kind kind, ActionLevelInfo info)
        {
            var now = Environment.TickCount64;
            var rowMin = (int)(missile.CreatureAction?.MinDamage ?? 0);
            var rowMax = Math.Max(rowMin, (int)(missile.CreatureAction?.MaxDamage ?? 0));

            GameEffect Effect(int typeId, long durationMs, bool announce) => new GameEffect
            {
                TypeId = typeId,
                EffectId = mapChannel != null ? GameEffectManager.Instance.NextEffectId(mapChannel) : 0,
                EffectLevel = info.Level,
                ActionId = info.ActionId,
                SourceId = attacker.EntityId,
                Source = attacker,
                SourceLevel = (int)attacker.Level,
                IsBuff = false,
                ExpiresTick = now + Math.Max(1, durationMs),
                AnnounceOnAttach = announce
            };

            switch (kind)
            {
                case Kind.DamageOverTime:
                {
                    if (rowMax <= 0)
                        return null;

                    var interval = Math.Max(1, info.Get(AbilityProperty.Interval, 1));
                    var duration = Math.Max(interval, info.Get(AbilityProperty.Duration, interval));
                    var typeId = module == "abilities.decay" ? DecayTypeId : AcidSpitTypeId;
                    var effect = Effect(typeId, duration * 1000L + 250, false);

                    effect.TickDamageMin = rowMin;
                    effect.TickDamageMax = rowMax;
                    effect.TickDamageType = (DamageType)info.Get(AbilityProperty.DamageType, (int)DamageType.Virulent);
                    effect.TickScaleType = 0;   // the row's numbers are already the creature's
                    effect.TickIntervalMs = interval * 1000;
                    effect.NextTickTick = now + effect.TickIntervalMs;
                    effect.Tooltip["dmgMin"] = rowMin;
                    effect.Tooltip["dmgMax"] = rowMax;
                    effect.Tooltip["interval"] = interval;

                    return effect;
                }

                case Kind.Hold:
                {
                    var typeId = module == "abilities.ai.xanxwebability" ? XanxWebTypeId : HunterNetTypeId;

                    return Effect(typeId, info.Get(AbilityProperty.Duration, 5) * 1000L, false);
                }

                case Kind.Blind:
                {
                    var typeId = info.Get(AbilityProperty.GameEffectId, QuillFlashTypeId);

                    return Effect(typeId, info.Get(AbilityProperty.EffectDurationMs, 2500), true);
                }

                case Kind.ResistDown:
                {
                    var min = info.Get(AbilityProperty.DebuffAmountMin);
                    var max = Math.Max(min, info.Get(AbilityProperty.DebuffAmountMax, min));
                    int amount;

                    lock (Random)
                        amount = Random.Next(min, max + 1);

                    if (amount <= 0)
                        return null;

                    var pheromone = module == "abilities.ai.attaharvesterpheromoneability";
                    var durationMs = pheromone
                        ? info.Get(AbilityProperty.EffectDurationMs, 180000)
                        : info.Get(AbilityProperty.Duration, 15) * 1000L;
                    var effect = Effect(pheromone ? PheromoneTypeId : GasCloudTypeId, durationMs, false);

                    effect.ResistDamageType = pheromone ? DamageType.Physical : DamageType.Ice;
                    effect.ResistModifier = -amount;
                    effect.Tooltip["debuffAmt"] = amount;

                    return effect;
                }

                case Kind.Polarity:
                {
                    var effect = Effect(PolarityFieldTypeId, info.Get(AbilityProperty.Duration, 30) * 1000L, false);

                    effect.ResistDamageType = (DamageType)info.Get(AbilityProperty.DamageType, (int)DamageType.Physical);
                    effect.ResistModifier = -PolarityVulnerability;

                    return effect;
                }

                case Kind.Nanites:
                {
                    if (rowMax <= 0)
                        return null;

                    var effect = Effect(ExplodingNanitesTypeId, info.Get(AbilityProperty.Duration, 30) * 1000L, false);

                    effect.OnDamagedMin = rowMin;
                    effect.OnDamagedMax = rowMax;
                    effect.OnDamagedType = (DamageType)info.Get(AbilityProperty.DamageType, (int)DamageType.Physical);
                    effect.TickScaleType = 0;
                    effect.OnDamagedCharges = Math.Max(1, info.Get(AbilityProperty.UseCount, 3));
                    effect.OnDamagedIntervalMs = Math.Max(0, info.Get(AbilityProperty.UseDropoff, 5)) * 1000;

                    return effect;
                }

                default:
                    return null;
            }
        }
    }
}
