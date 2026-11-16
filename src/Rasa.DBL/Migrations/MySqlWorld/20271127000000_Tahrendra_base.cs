using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Tahrendra Base's AFS, Bane, bosses and three missions, and the seed's Mires Bane on the map
    /// made the base's own (TahrendraBaseContent). Data only, in plain SQL: no model of its own.
    ///
    /// Down takes the missions out, then the rows, and puts back the pools it changed.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271127000000_Tahrendra_base")]
    public class Tahrendra_base : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in TahrendraBaseContent.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in TahrendraBaseContent.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
