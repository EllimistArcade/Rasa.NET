using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Services.Preloader
{
    using Structures.World;

    /// <summary>
    /// The four bots the Engineer's Bot Construction ability builds, seeded as ordinary creature
    /// rows so <c>.minion</c> can spawn one before there is an ability framework to summon them.
    ///
    /// The entity classes are the client's own, and the tooltip for
    /// <c>T4_ENGINEER_BOT_CONSTRUCTION</c> names them in pump order: Flame, Rocket, Repair,
    /// Shield, Multi. The first four exist as <c>Ability_NeoBot_*</c> classes carrying the
    /// Creature augmentation, which is what <see cref="CreatureEntry"/> rows need to be
    /// spawnable. Multi Bot has a name in the creature-name table and no class of its own, so it
    /// is not here.
    ///
    /// <c>name_id</c> is 0 deliberately. CreatureInfoPacket writes None for 0, and the client
    /// then falls back to the entity class's display name - which is already "Flame Bot",
    /// "Rocket Bot" and so on - rather than looking up a creature-name row that does not exist.
    ///
    /// The stats are placeholders. What the 2009 server gave a bot scaled off its master's level
    /// and the pump used, and none of that survives in client data; these are flat values chosen
    /// to be survivable enough to walk around behind a player during testing. They are not a
    /// reconstruction of anything.
    /// </summary>
    public class MinionCreaturePreloader : PreloaderBase, IPreloader
    {
        /// <summary>
        /// First id of the block reserved for minion test creatures. The service and mission NPCs
        /// hold 500001..510202, so this sits clear of them.
        /// </summary>
        public const uint IdMin = 600001;

        /// <summary>Last id in the block, so the migration can purge exactly what it seeded.</summary>
        public const uint IdMax = 600004;

        public void Preload(MigrationBuilder migrationBuilder)
        {
            Insert(migrationBuilder, CreatureEntry.TableName, typeof(CreatureEntry));
        }

        // id, comment, class_id, faction, level, max_hp, name_id, run_speed, walk_speed, action1..8
        //
        // faction 1 is AFS: a minion on the player's side, so it neither aggroes its master nor
        // gets shot by them. action 2 is "range attack afs light soldier" from creature_action -
        // 1..20 m, 800 ms cooldown, 10-15 damage - which is the closest thing in the table to a
        // bot plinking at what its owner is fighting.
        protected override IEnumerable<object[]> GetRows()
        {
            yield return new object[] { 600001, "Flame Bot (minion test)", 25539, 1, 10, 500, 0, 9, 5, 2, 0, 0, 0, 0, 0, 0, 0 };
            yield return new object[] { 600002, "Rocket Bot (minion test)", 25540, 1, 10, 500, 0, 9, 5, 2, 0, 0, 0, 0, 0, 0, 0 };
            yield return new object[] { 600003, "Shield Bot (minion test)", 25541, 1, 10, 500, 0, 9, 5, 2, 0, 0, 0, 0, 0, 0, 0 };
            yield return new object[] { 600004, "Repair Bot (minion test)", 25542, 1, 10, 500, 0, 9, 5, 2, 0, 0, 0, 0, 0, 0, 0 };
        }
    }
}
