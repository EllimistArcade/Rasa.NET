using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.MySqlWorld
{
    using Services.Preloader;
    using Structures.World;

    /// <summary>
    /// The named mission NPCs whose place a mission sentence states outright - "Report to Field
    /// Lieutenant Brody who is stationed at the Walk of Giants" - seeded at the client's map marker
    /// for that place, snapped to the walkable navmesh: 202 NPCs across the battlefields and
    /// instances, with the npc_package ids the client's objectiveconversation table gives for
    /// 132 of them. Data only - no schema changes - so the model is unchanged from Add_recipe.
    ///
    /// Down deletes the closed id range these rows occupy, not "id > base": creature, spawnpool
    /// and npc_package are identity columns, so rows inserted at runtime are allocated past the
    /// block and an open-ended delete would take them too.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Add_mission_npcs : Migration
    {
        private const uint IdMin = 510001;
        private const uint IdMax = 510202;

        private readonly ICollection<IPreloader> _preloaders = new List<IPreloader>
        {
            new MissionNpcCreaturePreloader(),
            new MissionNpcAppearancePreloader(),
            new MissionNpcSpawnpoolPreloader(),
            new MissionNpcPackagePreloader()
        };

        private static readonly string[] Tables =
        {
            NpcPackageEntry.TableName,
            SpawnPoolEntry.TableName,
            CreatureAppearanceEntry.TableName,
            CreatureEntry.TableName
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var preloader in _preloaders)
            {
                preloader.Preload(migrationBuilder);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"delete from {table} where id between {IdMin} and {IdMax};");
            }
        }
    }
}
