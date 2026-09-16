using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEPBMmanager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTrainStoresWithSpares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainBronze",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "TrainLeather",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "TrainMithril",
                table: "Armies");

            migrationBuilder.RenameColumn(
                name: "TrainTimber",
                table: "Armies",
                newName: "SpareWeapons");

            migrationBuilder.RenameColumn(
                name: "TrainSteel",
                table: "Armies",
                newName: "SpareArmour");

            migrationBuilder.AddColumn<string>(
                name: "SpareArmourMaterial",
                table: "Armies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SpareWeaponsMaterial",
                table: "Armies",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpareArmourMaterial",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "SpareWeaponsMaterial",
                table: "Armies");

            migrationBuilder.RenameColumn(
                name: "SpareWeapons",
                table: "Armies",
                newName: "TrainTimber");

            migrationBuilder.RenameColumn(
                name: "SpareArmour",
                table: "Armies",
                newName: "TrainSteel");

            migrationBuilder.AddColumn<int>(
                name: "TrainBronze",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TrainLeather",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TrainMithril",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
