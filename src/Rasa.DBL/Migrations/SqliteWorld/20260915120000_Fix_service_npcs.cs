using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.SqliteWorld
{
    using Services.Preloader;
    using Structures.World;

    /// <summary>
    /// Re-seeds the service NPCs, correcting three faults in Add_service_npcs, which has already
    /// run on live databases and so cannot be fixed in place.
    ///
    /// One NPC can carry two map markers - the base medic is both "Hospital: Fort Intrepid"
    /// and "Medical Vendor: Fort Intrepid" - and both were generated, 3 to 5 m apart, so 94
    /// counters had two vendors on them. name_id was 0, which makes the client fall back to
    /// the entity class's display name and call every one of them "Human". run_speed and
    /// walk_speed were 9 and 5, and since CreatureManager puts every creature into wander on
    /// spawn with no exemption for NPCs, they walked off their posts.
    ///
    /// The preloaders now carry the corrected 325 rows, so this deletes the whole of the
    /// original 500001..500419 block and runs them again. Data only - no schema changes.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Fix_service_npcs : Migration
    {
        private const uint IdMin = 500001;
        private const uint IdMax = 500325;
        private const uint PurgeMax = 500419;

        private readonly ICollection<IPreloader> _preloaders = new List<IPreloader>
        {
            new ServiceNpcCreaturePreloader(),
            new ServiceNpcAppearancePreloader(),
            new ServiceNpcSpawnpoolPreloader(),
            new ServiceNpcVendorPreloader(),
            new ServiceNpcVendorItemPreloader()
        };

        private static readonly string[] Tables =
        {
            VendorItemEntry.TableName,
            VendorEntry.TableName,
            SpawnPoolEntry.TableName,
            CreatureAppearanceEntry.TableName,
            CreatureEntry.TableName
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"delete from {table} where id between {IdMin} and {PurgeMax};");
            }

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
