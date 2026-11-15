using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// The Caves of Donn's Chantry tunnel egg cluster past the cave-in, out of the narrows before Undone Fairway, its
    /// Bugged to Death marker with it (DonnEggClusterNarrows). Data only, in plain SQL: no model of its own.
    ///
    /// Down puts both back.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271125000000_Donn_egg_cluster_narrows")]
    public class Donn_egg_cluster_narrows : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in DonnEggClusterNarrows.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in DonnEggClusterNarrows.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
