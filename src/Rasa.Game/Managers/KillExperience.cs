using System;

namespace Rasa.Managers
{
    /// <summary>
    /// What a creature's kill is worth in experience. The client keeps the numbers for the server
    /// (shared/gameconstants.py), which nothing in the client reads:
    ///
    ///     BASE_KILL_XP = 62.5
    ///     DANGER_PENALTY_VALUE = 0.2
    ///     DANGER_PENALTY_LEVELDIFF_MIN = 5
    ///     DANGER_PENALTY_LEVELDIFF_MAX = 9
    ///
    /// The formula BASE_KILL_XP went into is not in the client. It is read here as the base of
    /// the client's own exponential level scaling, the one an amount of SCALE_ACTOR_EXP goes by
    /// (shared/scaling.py ScaleExponentialAmount: "every scaleBasis levels ... doubles", "a
    /// scaleBasis of 8.0 is standard"), by the creature's level: int(62.5 x 2^((level - 1) / 8)).
    /// A level 1 creature is worth 62, a 9 125, a 17 250, a 25 500, a 33 1000, a 41 2000 and a 49
    /// 4000. That keeps a kill at about a fiftieth to a seventieth of a mission of its level all
    /// the way up, where the server's own level x 100 (give or take 10%, ours, now gone) was an
    /// eighth of a mission at level 5 and an eighteenth at 30.
    ///
    /// The danger penalty is each player's: a creature too far below them is no danger and worth
    /// less to them. Read as 20% less for each level from DANGER_PENALTY_LEVELDIFF_MIN below
    /// the player - 80% at five below, 60% at six, 40% at seven, 20% at eight - and nothing from
    /// DANGER_PENALTY_LEVELDIFF_MAX, where the five steps of 0.2 have taken it all. Ours, as the
    /// client says no more.
    ///
    /// The creature's XP_MODIFIER flag scales the whole (CreatureExperience), and the squad's
    /// share, its bonuses and the penalty are each sharer's (KillShares).
    /// </summary>
    public static class KillExperience
    {
        /// <summary>BASE_KILL_XP: a level 1 creature's kill.</summary>
        public const double BaseKillXp = 62.5;

        /// <summary>The client's standard scaleBasis: levels over which an amount doubles (ScaleExponentialAmount).</summary>
        public const double ScaleBasis = 8.0;

        /// <summary>DANGER_PENALTY_VALUE: what each level of the penalty takes off.</summary>
        public const double DangerPenaltyValue = 0.2;

        /// <summary>DANGER_PENALTY_LEVELDIFF_MIN: levels below the player the penalty starts at.</summary>
        public const int DangerLevelDiffMin = 5;

        /// <summary>DANGER_PENALTY_LEVELDIFF_MAX: levels below the player a creature is worth nothing from.</summary>
        public const int DangerLevelDiffMax = 9;

        /// <summary>The experience a creature of this level is worth: int(BASE_KILL_XP x 2^((level - 1) / 8)), level 1 at the least.</summary>
        public static uint BaseFor(uint level)
        {
            var from = Math.Max(1u, level);

            return (uint)(BaseKillXp * Math.Pow(2, (from - 1) / ScaleBasis));
        }

        /// <summary>The share of a kill a player of this level is paid for a creature of that level: 1 but for one far below them.</summary>
        public static double DangerFactor(int playerLevel, uint creatureLevel)
        {
            if (creatureLevel == 0 || playerLevel <= 0)
                return 1.0;

            var below = playerLevel - (int)creatureLevel;

            if (below < DangerLevelDiffMin)
                return 1.0;

            if (below >= DangerLevelDiffMax)
                return 0.0;

            return Math.Max(0.0, 1.0 - DangerPenaltyValue * (below - DangerLevelDiffMin + 1));
        }

        /// <summary>An amount of a kill's experience after a player's danger penalty, rounded down.</summary>
        public static uint AfterDanger(uint amount, int playerLevel, uint creatureLevel)
        {
            var factor = DangerFactor(playerLevel, creatureLevel);

            return factor >= 1.0 ? amount : (uint)Math.Floor(amount * factor + 1e-9);
        }
    }
}
