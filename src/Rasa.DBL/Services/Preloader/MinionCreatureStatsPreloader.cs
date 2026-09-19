using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Services.Preloader
{
    using Structures.World;

    /// <summary>
    /// Stats for the four minion test creatures.
    ///
    /// Without a row here <c>CreatureManager.CreateCreature</c> falls through to its 100-health
    /// default, which would disagree with the 500 in <c>max_hp</c> on the creature row. These
    /// exist so the two numbers say the same thing; see <see cref="MinionCreaturePreloader"/> for
    /// why the values themselves are placeholders.
    /// </summary>
    public class MinionCreatureStatsPreloader : PreloaderBase, IPreloader
    {
        public void Preload(MigrationBuilder migrationBuilder)
        {
            Insert(migrationBuilder, CreatureStatEntry.TableName, typeof(CreatureStatEntry));
        }

        // id, body, mind, spirit, health, armor
        protected override IEnumerable<object[]> GetRows()
        {
            yield return new object[] { 600001, 15, 15, 15, 500, 150 };
            yield return new object[] { 600002, 15, 15, 15, 500, 150 };
            yield return new object[] { 600003, 15, 15, 15, 500, 150 };
            yield return new object[] { 600004, 15, 15, 15, 500, 150 };
        }
    }
}
