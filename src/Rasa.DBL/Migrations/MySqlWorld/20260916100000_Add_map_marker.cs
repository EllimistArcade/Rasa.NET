using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.MySqlWorld
{
    using Services.Preloader;

    /// <summary>
    /// The map screen's status markers, tied to the server objects whose state they show.
    ///
    /// Keyed on the client's own marker entity id, because that is what MapMarkerInfo is keyed
    /// by: the ids are compiled into generated.client.uimapmarker and the map window looks each
    /// marker's state up under the id it drew the marker with. Nothing on this server is ever
    /// spawned with one.
    /// </summary>
    public partial class Add_map_marker : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_marker",
                columns: table => new
                {
                    marker_entity_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    map_context_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    marker_type = table.Column<uint>(type: "int unsigned", nullable: false),
                    object_kind = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    object_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    match_distance = table.Column<double>(type: "double", nullable: false),
                    comment = table.Column<string>(type: "varchar(64)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_map_marker", x => new { x.marker_entity_id, x.map_context_id });
                });

            // Every read is one map's markers, on a player entering it.
            migrationBuilder.CreateIndex(
                name: "map_marker_index_map_context_id",
                table: "map_marker",
                column: "map_context_id");

            new MapMarkerPreloader().Preload(migrationBuilder);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_marker");
        }
    }
}
