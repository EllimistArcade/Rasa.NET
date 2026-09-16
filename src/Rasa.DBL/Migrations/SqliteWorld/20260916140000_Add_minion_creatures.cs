using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.SqliteWorld
{
    using Services.Preloader;
    using Structures.World;

    /// <summary>
    /// Seeds the four Bot Construction bots as spawnable creatures, so the minion command system
    /// has something to command before there is an ability framework to summon one.
    ///
    /// Data only - no schema changes. The rows occupy 600001..600004, a block nothing else uses,
    /// and Down removes exactly that block.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Add_minion_creatures : Migration
    {
        private readonly ICollection<IPreloader> _preloaders = new List<IPreloader>
        {
            new MinionCreaturePreloader(),
            new MinionCreatureStatsPreloader()
        };

        private static readonly string[] Tables =
        {
            CreatureStatEntry.TableName,
            CreatureEntry.TableName
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-runnable: clear the block first so a partially seeded database does not collide.
            foreach (var table in Tables)
                migrationBuilder.Sql($"delete from {table} where id between {MinionCreaturePreloader.IdMin} and {MinionCreaturePreloader.IdMax};");

            foreach (var preloader in _preloaders)
                preloader.Preload(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
                migrationBuilder.Sql($"delete from {table} where id between {MinionCreaturePreloader.IdMin} and {MinionCreaturePreloader.IdMax};");
        }
    }
}
