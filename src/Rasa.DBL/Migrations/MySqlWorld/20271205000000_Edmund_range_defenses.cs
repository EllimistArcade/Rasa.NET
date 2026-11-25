using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Edmund Range's control points' turrets and force fields (EdmundRangeDefenses): a holder's
    /// turret and a Bane turret on each of the sixteen round bases the points' fences have, and
    /// a force field in each of their twelve gates, linked to their points. Data only, in plain
    /// SQL: no model of its own.
    ///
    /// Down takes out the rows and their links.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271205000000_Edmund_range_defenses")]
    public class Edmund_range_defenses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in EdmundRangeDefenses.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in EdmundRangeDefenses.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
