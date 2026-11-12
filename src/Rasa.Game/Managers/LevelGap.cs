using System;

namespace Rasa.Managers
{
    using Structures;

    /// <summary>
    /// Damage across a wide level gap. The client keeps the numbers for the server
    /// (shared/gameconstants.py), which nothing in the client reads:
    ///
    ///     LEVELDIFF_DAMAGEMOD_LEVEL_MIN = 9
    ///     LEVELDIFF_DAMAGEMOD_LEVEL_MAX = 12
    ///     LEVELDIFF_DAMAGEMOD_MODIFIER = 0.9 / (LEVELDIFF_DAMAGEMOD_LEVEL_MAX - LEVELDIFF_DAMAGEMOD_LEVEL_MIN)
    ///
    /// Read here as: an attacker up to nine levels below its target does its full damage; each
    /// level past that takes MODIFIER (30%) off, so ten below does 70%, eleven 40%, and twelve or
    /// more a tenth - the 0.9 spread over the three levels, down to what is left at MAX. A
    /// player cannot take on a creature far above them, and a low creature does next to nothing
    /// to a player far above it. Which way round it goes (the lower attacker on the higher
    /// target), and that it is every attacker's, a creature's and a player's alike, are ours; an
    /// attacker as high as its target or higher is untouched.
    ///
    /// The factor is on what the hit does before shields and armour - weapon fire
    /// (MissileManager), every ability, bomb, tick and blast (ActorManager.Damage) - and a hit
    /// that did anything still does at least 1. A hit with no source, or whose source has no
    /// level (the world's own: lava, a placed blast), is untouched.
    /// </summary>
    public static class LevelGap
    {
        /// <summary>LEVELDIFF_DAMAGEMOD_LEVEL_MIN: levels below its target an attacker still does full damage at.</summary>
        public const int LevelMin = 9;

        /// <summary>LEVELDIFF_DAMAGEMOD_LEVEL_MAX: levels below its target at which an attacker is down to its floor.</summary>
        public const int LevelMax = 12;

        /// <summary>LEVELDIFF_DAMAGEMOD_MODIFIER: what each level past LevelMin takes off.</summary>
        public const double Modifier = 0.9 / (LevelMax - LevelMin);

        /// <summary>An actor's level: a player's or a creature's; 0 for anything else.</summary>
        public static int LevelOf(Actor actor) => actor switch
        {
            Manifestation player => player.Level,
            Creature creature => (int)creature.Level,
            _ => 0
        };

        /// <summary>The share of its damage an attacker this many levels below its target does.</summary>
        public static double Factor(int levelsBelow)
        {
            if (levelsBelow <= LevelMin)
                return 1.0;

            return 1.0 - Modifier * (Math.Min(levelsBelow, LevelMax) - LevelMin);
        }

        /// <summary>The share of its damage the source does to the target for the levels between them; 1 when either has no level.</summary>
        public static double Factor(Actor source, Actor target)
        {
            var from = LevelOf(source);
            var to = LevelOf(target);

            return from <= 0 || to <= 0 ? 1.0 : Factor(to - from);
        }

        /// <summary>A hit from the source on the target, after the level gap between them.</summary>
        public static int Scale(int amount, Actor source, Actor target)
        {
            var factor = Factor(source, target);

            if (amount <= 0 || factor >= 1.0)
                return amount;

            return Math.Max(1, (int)Math.Round(amount * factor, MidpointRounding.AwayFromZero));
        }
    }
}
