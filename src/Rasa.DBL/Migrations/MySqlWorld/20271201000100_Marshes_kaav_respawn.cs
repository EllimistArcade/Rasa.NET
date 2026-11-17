using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

using Rasa.Context.World;
using Rasa.Services.Preloader.Missions.Marshes;

namespace Rasa.Migrations.MySqlWorld
{
    /// <summary>
    /// Conscript Kaav, the Marshes' Bane Traitor, back five minutes after he is killed, not two seconds
    /// (MarshesKaavRespawnV1). Data only, in plain SQL: no model of its own.
    ///
    /// Down gives him two seconds again, if he still has five minutes.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    [DbContext(typeof(MySqlWorldContext))]
    [Migration("20271201000100_Marshes_kaav_respawn")]
    public class Marshes_kaav_respawn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MarshesKaavRespawnV1.Up())
                migrationBuilder.Sql(sql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in MarshesKaavRespawnV1.Down())
                migrationBuilder.Sql(sql);
        }
    }
}
