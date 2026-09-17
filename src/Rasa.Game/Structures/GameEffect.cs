using System;

namespace Rasa.Structures
{
    using Data;

    /// <summary>
    /// A buff, debuff or looping gesture on an actor. TypeId is the client's gameeffectdata id
    /// (which picks the client-side effect class and its visuals), EffectId identifies this
    /// instance to the client the way an entity id identifies an entity, and EffectLevel is the
    /// pump level it was applied at. Timing is in Environment.TickCount64 so it does not depend
    /// on how often the effect worker happens to run.
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

        /// <summary>Environment.TickCount64 at which it wears off; long.MaxValue for "until detached".</summary>
        public long ExpiresTick { get; set; }

        /// <summary>Milliseconds between ticks, for effects that do something on a schedule; 0 for none.</summary>
        public int TickIntervalMs { get; set; }

        /// <summary>Environment.TickCount64 of the next tick.</summary>
        public long NextTickTick { get; set; }

        /// <summary>Movement speed as a percent of normal while the effect is on: 120 is a fifth faster. 0 means no change.</summary>
        public int MovementModifierPercent { get; set; }

        /// <summary>Percent of the actor's maximum chi (adrenaline) taken per second while the effect is on; the effect ends when the bar is empty.</summary>
        public double AdrenalineDrainPercentPerSecond { get; set; }

        /// <summary>The fraction of a point of drain carried to the next tick, so 1.5 a second takes 3 every two seconds and not 2.</summary>
        public double DrainCarry { get; set; }

        /// <summary>
        /// The client may ask for this effect to be removed (RequestDetachGameEffect) - a toggle
        /// like sprint, or any buff the player right-clicks away. Debuffs stay.
        /// </summary>
        public bool AllowDetach { get; set; }

        public bool IsExpired => Environment.TickCount64 >= ExpiresTick;

        public bool TickDue => TickIntervalMs > 0 && Environment.TickCount64 >= NextTickTick;

        /// <summary>Whole seconds left, for the client's tooltip; capped so a held effect does not show an absurd number.</summary>
        public int RemainingSeconds => ExpiresTick == long.MaxValue ? 0 : (int)Math.Max(0, (ExpiresTick - Environment.TickCount64) / 1000);
    }
}
