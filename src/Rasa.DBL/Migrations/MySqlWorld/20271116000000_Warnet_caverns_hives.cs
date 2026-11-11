using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Two of the Warnet Caverns' Hanging Hives move out of the rock to open roof over Bug City's floor
    /// (WarnetCavernsHives). Data only, in plain SQL: no model of its own.
    ///
    /// Down puts them back.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271116000000_Warnet_caverns_hives")]
    public class Warnet_caverns_hives : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in WarnetCavernsHives.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in WarnetCavernsHives.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
