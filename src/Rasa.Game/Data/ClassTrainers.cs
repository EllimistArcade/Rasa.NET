using System.Collections.Generic;

namespace Rasa.Data
{
    /// <summary>
    /// The creatures that train a character into a new class.
    ///
    /// Nothing in the world data marks one. The seventeen were placed from the client's own map
    /// markers by tools/gen_service_npcs.py, which knew their role and wrote it only into the
    /// creature row's comment - a developer field, not something to match on. The three fields
    /// that might have served all fail: the entity class they are built on is Vendor_Human_Male,
    /// a generic vendor body shared with the shop NPCs; their name ids are one per trainer and
    /// one of them (6918) is also used by a non-trainer; and the Vendor augmentation they carry
    /// from that class says shop, not trainer. The client's augmentation list has no Trainer
    /// entry at all.
    ///
    /// So the ids are named here, the way the clan master is recognised by its name id in
    /// CreatureManager. They are stable: the generator assigns them from 500000 up and nothing
    /// hand-seeded reaches that range. When the creature table next takes a migration this
    /// belongs in a column on it, and this file goes away.
    /// </summary>
    public static class ClassTrainers
    {
        private static readonly HashSet<uint> Ids = new HashSet<uint>
        {
            500001,     // Class Trainer: Daghda's Urn
            500002,     // Class Trainer: Twin Pillars
            500012,     // Class Trainer
            500046,     // Class Trainer
            500080,     // Trainer: Nyxroq Post
            500093,     // Class Trainer
            500112,     // Class Trainers: Irendas Penal Colony
            500113,     // Class Trainers: Irendas Penal Colony
            500114,     // Class Trainers: Irendas Penal Colony
            500146,     // Trainer: Foreas Base
            500180,     // Trainer: New Cumbria
            500229,     // Class Trainer
            500248,     // Class Trainer
            500274,     // Class Trainer
            500300,     // Class Trainer
            500318,     // Class Trainer
            500324      // Class Trainer
        };

        public static int Count => Ids.Count;

        public static bool Trains(uint creatureDbId)
        {
            return Ids.Contains(creatureDbId);
        }
    }
}
