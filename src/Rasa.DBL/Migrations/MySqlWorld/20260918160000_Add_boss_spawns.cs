using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

using JetBrains.Annotations;

namespace Rasa.Migrations.MySqlWorld
{
    using Services.Preloader;
    using Structures.World;

    /// <summary>
    /// 65 named bosses put where the mining says they stand, across 16 zones: Abyss 1, Ashen Desert 6, Bootcamp 3, Caves of Donn 1, Crater Lake Research 1, Crucible 2, Divide 5, Howling Maw 2, Incline 6, Marshes 9, Palisades 10, Plains 4, Plateau 4, Pools 8, Thunderhead 2, Wilderness 1.
    ///
    /// Before this, spawn pools existed on one map - Concordia Wilderness - so every other zone was
    /// empty of bosses. Each boss gets a creature row carrying the client's own creature name id,
    /// so the client draws its real name, the family's _Boss entity class, faction 0, and a
    /// creature_action row that already existed for that family; a creature_stats row; and a pool
    /// of one with a 500 second respawn, the interval the fork's existing named world bosses use.
    ///
    /// Where each field comes from is recorded per boss in world/boss_spawns.csv, and the generator
    /// that wrote these rows is world/tools/npcmine/gen_boss_spawns.py. In short: the name and the
    /// position are sourced - the client's name table, and the Ellatha fan site's live-game
    /// coordinates, a map label or a mission statement, every one of them put on the walkable
    /// navmesh here - while the entity class, the level and the stats are ours. 60 of the levels
    /// are an estimate from the zone's band, which the generator marks as such and holds in one
    /// table so they can be retuned in one place.
    ///
    /// 65 bosses already had a creature row that nothing spawned, and get a pool against the id
    /// already there rather than a second row. Bosses already spawned in Wilderness are left alone.
    ///
    /// Three bosses were already in the world under a placeholder name id - Proctor Fulgor as
    /// "Light Bender Boss - 1", Arioch as "Xanx Matriarch", the Fithik Hive Monarch as "Fithik
    /// Monarch". Their rows are theirs, only the name was a stand-in, so those rows keep their
    /// place and their spawn and just take the client's real name id.
    ///
    /// Down deletes the closed id range these rows occupy, not "id > base": creature and spawnpool
    /// are identity columns, so rows inserted at runtime are allocated past the block and an
    /// open-ended delete would take them too.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UsedImplicitly]
    public partial class Add_boss_spawns : Migration
    {
        private const uint IdMin = 520001;
        private const uint IdMax = 520999;

        private readonly ICollection<IPreloader> _preloaders = new List<IPreloader>
        {
            new BossCreaturePreloader(),
            new BossCreatureStatsPreloader(),
            new BossSpawnpoolPreloader()
        };

        private static readonly string[] Tables =
        {
            SpawnPoolEntry.TableName,
            CreatureStatEntry.TableName,
            CreatureEntry.TableName
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var preloader in _preloaders)
            {
                preloader.Preload(migrationBuilder);
            }

            // Arioch, which the client reads as its real name instead of a placeholder
            migrationBuilder.Sql($"update {CreatureEntry.TableName} set name_id = 6727 where id = 77 and name_id = 476;");
            // Fithik Hive Monarch, which the client reads as its real name instead of a placeholder
            migrationBuilder.Sql($"update {CreatureEntry.TableName} set name_id = 8522 where id = 89 and name_id = 375;");
            // Proctor Fulgor, which the client reads as its real name instead of a placeholder
            migrationBuilder.Sql($"update {CreatureEntry.TableName} set name_id = 10100 where id = 76 and name_id = 510;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"update {CreatureEntry.TableName} set name_id = 476 where id = 77 and name_id = 6727;");
            migrationBuilder.Sql($"update {CreatureEntry.TableName} set name_id = 375 where id = 89 and name_id = 8522;");
            migrationBuilder.Sql($"update {CreatureEntry.TableName} set name_id = 510 where id = 76 and name_id = 10100;");

            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"delete from {table} where id between {IdMin} and {IdMax};");
            }
        }
    }
}
