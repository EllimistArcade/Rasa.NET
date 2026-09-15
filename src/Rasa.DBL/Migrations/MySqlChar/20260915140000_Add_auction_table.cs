using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Migrations.MySqlChar
{
    /// <summary>
    /// The auction table: one row per item a character has listed, holding what the auction adds
    /// on top of the item itself - seller, price, deposit paid and when the listing started.
    ///
    /// The item stays in the items table with a character_inventory row of type 11
    /// (AuctionInventory), so its entity is created on the client at login like any other item
    /// the character owns, and item_id is this table's key: an item is in at most one auction.
    /// </summary>
    public partial class Add_auction_table : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auction",
                columns: table => new
                {
                    item_id = table.Column<uint>(type: "int(11) unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    seller_id = table.Column<uint>(type: "int(11) unsigned", nullable: false),
                    seller_name = table.Column<string>(type: "varchar(64)", nullable: false),
                    price = table.Column<uint>(type: "int(11) unsigned", nullable: false),
                    deposit = table.Column<uint>(type: "int(11) unsigned", nullable: false),
                    duration_hours = table.Column<uint>(type: "int(11) unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auction", x => x.item_id);
                });

            // Every "My Auctions" request and every max-auctions check reads by seller.
            migrationBuilder.CreateIndex(
                name: "auction_index_seller_id",
                table: "auction",
                column: "seller_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auction");
        }
    }
}
