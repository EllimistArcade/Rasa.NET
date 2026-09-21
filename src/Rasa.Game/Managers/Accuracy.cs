using System;

namespace Rasa.Managers
{
    using Structures;

    /// <summary>
    /// The aiming bead, kept on the server the way the client keeps it, so that the server knows
    /// when a shot was fired at full bead. The client never says: RequestWeaponAttack carries the
    /// action, its argument and the target, and the bead lives only in the client's
    /// Manifestation.UpdateAccuracy (client/augmentations/manifestation.py). This is that code,
    /// with the same constants from shared/gameconstants.py:
    ///
    ///  - accuracy runs from 0 to 100; the bead is full above 99 ("accuracyRatio > 0.99" in
    ///    reticlewindow.py, which is what shows the full-bead reticle);
    ///  - it climbs towards a ceiling at the weapon's aim rate per millisecond, twice that while
    ///    crouched (CROUCHED_ACCURACY_MOD 2.0), and falls at ACCURACY_LOSS_RATE (50 a second) when
    ///    it is above the ceiling;
    ///  - the ceiling is 100 crouched (CROUCHED_ACCURACY_MAX) and 80 standing still or walking
    ///    (STOPPED / SLOW_ACCURACY_MAX), 64 running (FAST_ACCURACY_MAX);
    ///  - each shot takes the weapon's recoil amount off it (baseweaponattack.py LocalDoAction);
    ///  - drawing a weapon starts it from 0 (actor.py, on an equipment update with a weapon).
    ///
    /// The rates are worked out again whenever what they depend on changes - crouching, the
    /// target, the weapon - as UpdateAccuracyRates is on the client. Running is not told apart
    /// from walking: only crouching lifts the ceiling past 99, so whether a standing player could
    /// have reached 80 or only 64 never decides whether a shot was at full bead.
    ///
    /// The aim rate and recoil come from the weapon template, which is also what the client is
    /// sent in WeaponInfo, so both ends run the same bead. Those two numbers are placeholders in
    /// the shipped data (1 and 1 on every row), which makes the bead close almost at once when
    /// crouched; correcting them per weapon corrects both ends together.
    /// </summary>
    public static class Accuracy
    {
        public const double FullBeadRatio = 0.99;           // reticlewindow.py
        public const double LossPerMs = 50.0 / 1000.0;      // ACCURACY_LOSS_RATE
        public const double CrouchedMax = 100;              // CROUCHED_ACCURACY_MAX
        public const double CrouchedRateMod = 2.0;          // CROUCHED_ACCURACY_MOD
        public const double StandingMax = 80;               // STOPPED_ACCURACY_MAX, SLOW_ACCURACY_MAX
        public const double StandingRateMod = 1.0;          // STOPPED_ACCURACY_MOD

        /// <summary>Where the bead is now, brought up to date from the last time it was looked at.</summary>
        public static double Current(Manifestation player, long now)
        {
            var elapsed = Math.Max(0, now - player.AccuracyUpdatedTick);
            var amount = elapsed * player.AccuracyRate;

            if (amount > 0 && player.AccuracyValue < player.AccuracyMax)
                player.AccuracyValue = Math.Min(player.AccuracyValue + amount, player.AccuracyMax);

            if (amount < 0 && player.AccuracyValue > player.AccuracyMax)
                player.AccuracyValue = Math.Max(player.AccuracyValue + amount, player.AccuracyMax);

            player.AccuracyUpdatedTick = now;

            return player.AccuracyValue;
        }

        public static bool IsFullBead(Manifestation player, long now) => Current(player, now) / 100.0 > FullBeadRatio;

        /// <summary>
        /// The ceiling and the rate towards it, worked out again: the player crouched or stood,
        /// changed target or weapon. The bead is brought up to date first, so the time before the
        /// change counts at the old rate.
        /// </summary>
        public static void UpdateRates(Manifestation player, double aimRate, long now)
        {
            Current(player, now);

            var crouched = player.IsCrouching;

            player.AccuracyMax = crouched ? CrouchedMax : StandingMax;

            if (player.AccuracyMax < player.AccuracyValue)
                player.AccuracyRate = -LossPerMs;
            else if (player.AccuracyMax > player.AccuracyValue)
                player.AccuracyRate = Math.Max(0, aimRate) * (crouched ? CrouchedRateMod : StandingRateMod);
        }

        /// <summary>A shot's recoil comes off the bead.</summary>
        public static void Recoil(Manifestation player, double recoil, double aimRate, long now)
        {
            Current(player, now);

            player.AccuracyValue = Math.Max(0, player.AccuracyValue - Math.Max(0, recoil));

            UpdateRates(player, aimRate, now);
        }

        /// <summary>A weapon drawn: the bead starts again from nothing.</summary>
        public static void Reset(Manifestation player, double aimRate, long now)
        {
            player.AccuracyValue = 0;
            player.AccuracyUpdatedTick = now;

            UpdateRates(player, aimRate, now);
        }
    }
}
