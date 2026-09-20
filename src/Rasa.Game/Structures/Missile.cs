namespace Rasa.Structures
{
    using Data;

    public class Missile
    {
        public int DamageA { get; set; }
        public int DamageB { get; set; }
        public ActionId ActionId { get; set; }
        public uint ActionArgId { get; set; }
        public bool IsAbility { get; set; }         // set to true to use PerformAbility instead of Windup/Recovery
        public ulong TargetEntityId { get; set; }    // the entityId of the destination (it is possible that the object does no more exist on arrival)
        public Actor TargetActor { get; set; }
        public Actor Source { get; set; }
        /// <summary>Percent of DamageA that skips armour and comes straight off health (Torqueshell and Injection Guns skills).</summary>
        public int ArmorBypassPercent { get; set; }
        /// <summary>The attack's damage type, as reported to the clients; 0 is treated as physical.</summary>
        public DamageType DamageType { get; set; }
        /// <summary>A melee swing rather than a shot: crouching helps a shot crit and helps a swing crit the one crouching.</summary>
        public bool IsMelee { get; set; }
        /// <summary>The shooter's crit chance in percent, worked out when it was fired; the target's part is added when it lands.</summary>
        public double CritChance { get; set; }
        /// <summary>Whether it landed as a critical hit, DamageA already multiplied.</summary>
        public bool IsCritical { get; set; }
        public long TriggerTime { get; set; }       // amount of milliseconds left before the missile is triggered, is decreased on every tick
        public MissileArgs Args = new MissileArgs();
    }
}
