using System.Collections.Generic;

namespace Rasa.Managers
{
    using Services.Preloader.Missions.Plateau;
    using Structures;

    /// <summary>
    /// Mission NPCs that stand where hostile creatures roam and take no part in their fights, from the moment they
    /// spawn: no creature seeks them out or turns on them (Creature.KeptFromCreatures), and they look for no one
    /// themselves (AggroRange 0). They are FRIENDLY, so no player fights them either.
    ///
    /// - The Holographic Projection Device at Vogren's Tomb (PlateauVogrenNpcsV1.Hologram), Mount Reverence: the
    ///   Tomb Miasmas roam 16 m from it, inside their 18 m aggro range, and killed it again and again (to-fix #39).
    ///   It was made NEUTRAL for that, which let players kill it for experience every two seconds
    ///   (PlateauVogrenHologramFriendlyV1).
    /// </summary>
    internal static class NpcsKeptFromCreatures
    {
        private static readonly HashSet<uint> Kept = new() { PlateauVogrenNpcsV1.Hologram };

        internal static bool Includes(uint creatureId) => Kept.Contains(creatureId);

        /// <summary>Keeps a freshly made creature out of creatures' fights, if it is one of these.</summary>
        internal static void Apply(Creature creature)
        {
            if (creature == null || !Includes(creature.DbId))
                return;

            creature.KeptFromCreatures = true;
            creature.AggroRange = 0f;
        }
    }
}
