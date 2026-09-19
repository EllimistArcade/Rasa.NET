using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.MySqlWorld
{
    using Services.Preloader;
    using Structures.World;

    /// <summary>
    /// The per-class class trainers, at the five places the client names them.
    ///
    /// The shipped maps place ten trainer pins and most are cluster labels, so the service-NPC
    /// pass could only put one generic trainer on each. uimapmarkertext holds 37 trainer texts
    /// and npctrainerdialoglanguage 80 four-line groups, and between them they name a single
    /// class at a single place 19 and 24 times over - agreeing, with no ids in common, on six
    /// classes at Alia Das and Twin Pillars and twelve at Fort Defiance and Torden Mires.
    ///
    /// Data only - no schema changes. Down deletes the closed range, not "id > base", because
    /// creature and spawnpool are identity columns and a row inserted at runtime lands above it.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Add_class_trainers : Migration
    {
        private const uint IdMin = 501001;
        private const uint IdMax = 501038;

        private readonly ICollection<IPreloader> _preloaders = new List<IPreloader>
        {
            new ClassTrainerCreaturePreloader(),
            new ClassTrainerAppearancePreloader(),
            new ClassTrainerSpawnpoolPreloader()
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var preloader in _preloaders)
                preloader.Preload(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[]
            {
                SpawnPoolEntry.TableName,
                CreatureAppearanceEntry.TableName,
                CreatureEntry.TableName
            })
            {
                migrationBuilder.Sql($"delete from {table} where id between {IdMin} and {IdMax};");
            }
        }
    }
}
