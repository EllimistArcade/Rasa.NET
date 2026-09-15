using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.MySqlWorld
{
    using Services.Preloader;
    using Structures.World;

    /// <summary>
    /// The service NPCs the client's map markers place: 419 vendors, trainers, auctioneers and
    /// medics across 69 maps. Data only - no schema changes - so the model is unchanged from
    /// 20260913180000_Add_map_link.
    ///
    /// Down deletes by id rather than emptying the tables, because these rows share them with
    /// the hand-seeded world data.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Add_service_npcs : Migration
    {
        private const uint IdBase = 500000;

        private readonly ICollection<IPreloader> _preloaders = new List<IPreloader>
        {
            new ServiceNpcCreaturePreloader(),
            new ServiceNpcAppearancePreloader(),
            new ServiceNpcSpawnpoolPreloader(),
            new ServiceNpcVendorPreloader(),
            new ServiceNpcVendorItemPreloader()
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
            foreach (var table in new[]
            {
                VendorItemEntry.TableName,
                VendorEntry.TableName,
                SpawnPoolEntry.TableName,
                CreatureAppearanceEntry.TableName,
                CreatureEntry.TableName
            })
            {
                migrationBuilder.Sql($"delete from {table} where id > {IdBase};");
            }
        }
    }
}
