using System;

namespace Rasa.Managers
{
    using Data;

    /// <summary>
    /// How long a shot is in the air, and what the server does about it.
    ///
    /// The client flies every shot itself. TargetedAction.GetDistanceDelay is the distance to the
    /// target over the weapon's velocity - the velocity column of the weapon class table, which
    /// both ends have, row for row - or over DEFAULT_PROJECTILE_VELOCITY (70 m/s,
    /// shared/gameconstants.py) where the class gives none. A negative velocity (-1: the Laser
    /// Torqueshell rifles, the Lightbender) is a shot that is there at once, the sum coming out
    /// before now. When the shot gets there the client plays what the recovery said of it: the
    /// number, the hit's effects, the death of a deathBlow, and the armour and health bars, which
    /// an update naming who did it (UpdateHealth's whoId, Actor.UpdateAttribute) leaves alone
    /// until that damage is announced.
    ///
    /// So the recovery is not something to hold back for the flight as a rule. Somebody else's
    /// shot - a creature's, another player's - starts on the client when its recovery arrives:
    /// the firing animation, the projectile, and the wait. Held on the server for the flight, it
    /// would be fired late and land later still. A creature's shot therefore resolves as it is
    /// fired, as it always did, and what it does to a player's bars waits on that player's
    /// client (MissileManager.StagedBy).
    ///
    /// A player's own shot is the other case. Their client starts its clock when the trigger is
    /// pulled and shows the result when both the shot has arrived and the server has answered,
    /// so an answer that comes as the shot lands is shown on time - and what the landing does
    /// in the world happens then too: the creature falls, and leaves its loot, when the round
    /// reaches it and not when it leaves the barrel.
    ///
    /// The limit is how long the shooter's client keeps the shot in hand. It gives a recovery to
    /// the action it has (Actor.Recv_PerformRecovery), and its own shot is that action for its
    /// windup and recovery - the attack argument's, 250 ms for a pistol, 500 for a rifle - or
    /// until the shot has arrived, if that is later (BaseActorAction.Finish waits on the
    /// strike), and no longer: the action is finished then whether the server has answered or
    /// not. A recovery that comes after it finds no action, and the client makes a new one of
    /// it and plays it - the firing animation and a second round out of the barrel, with the
    /// one hit the server counted. Holding a shot for its flight puts its answer after the
    /// strike, so a shot is held only if its answer still comes inside the windup and recovery:
    /// its flight and the allowance for a queue walked once a pass and an uneven network
    /// (ManifestationManager.ShotTolerance) within them (InHandMs), and within the weapon's
    /// refire. The refire alone was the limit, and every pistol shot held - a pistol's refire
    /// is 300 to 500 ms - came back after its 250 ms and was fired twice on the shooter's
    /// screen; so was a rifle's past 17 m. Held now: every shot of a launcher or a shotgun at
    /// any range it reaches, a rifle's to 17 m, a pistol's or an injection gun's never.
    /// Anything else resolves as it is fired (MissileManager.ResolveShotsDueNow).
    /// </summary>
    public static class ShotFlight
    {
        /// <summary>DEFAULT_PROJECTILE_VELOCITY: metres a second for a weapon class that gives no velocity of its own (0).</summary>
        public const double DefaultVelocity = 70.0;

        /// <summary>
        /// Milliseconds a shot takes over this distance from a weapon class of this velocity:
        /// the client's own figure. Nothing for a negative velocity.
        /// </summary>
        public static int Ms(float distance, int velocity)
        {
            if (!float.IsFinite(distance) || distance <= 0 || velocity < 0)
                return 0;

            return (int)Math.Round(distance / (velocity > 0 ? velocity : DefaultVelocity) * 1000.0);
        }

        /// <summary>
        /// How long the server holds a player's shot before it lands: its flight, if its answer
        /// still reaches the shooter's client while it has the shot in hand and before the
        /// weapon can be fired again, and otherwise not at all.
        /// </summary>
        /// <param name="inHandMs">The attack's windup and recovery (InHandMs); 0 when not known, and only the refire limits it.</param>
        public static int HeldMs(float distance, int velocity, long refireMs, long inHandMs = 0)
        {
            var flight = Ms(distance, velocity);
            var limit = inHandMs > 0 ? Math.Min(refireMs, inHandMs) : refireMs;

            return flight > 0 && flight + ManifestationManager.ShotTolerance <= limit ? flight : 0;
        }

        /// <summary>
        /// How long the shooter's client has a shot of this attack in hand: its argument's
        /// windup and recovery, as the client's action plays them. 0 when the server has no
        /// level for it.
        /// </summary>
        public static long InHandMs(ActionId actionId, uint actionArgId)
        {
            if (AbilityManager.Instance == null || !AbilityManager.Instance.TryGetLevel(actionId, actionArgId, out var info))
                return 0;

            return Math.Max(0, info.WindupMs) + Math.Max(0, info.RecoveryMs);
        }
    }
}
