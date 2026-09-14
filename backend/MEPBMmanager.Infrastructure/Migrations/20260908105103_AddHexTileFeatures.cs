using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEPBMmanager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHexTileFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasFord",
                table: "HexTiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasMajorRiver",
                table: "HexTiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasMinorRiver",
                table: "HexTiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasRoad",
                table: "HexTiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasFord",
                table: "HexTiles");

            migrationBuilder.DropColumn(
                name: "HasMajorRiver",
                table: "HexTiles");

            migrationBuilder.DropColumn(
                name: "HasMinorRiver",
                table: "HexTiles");

            migrationBuilder.DropColumn(
                name: "HasRoad",
                table: "HexTiles");
        }
    }
}
