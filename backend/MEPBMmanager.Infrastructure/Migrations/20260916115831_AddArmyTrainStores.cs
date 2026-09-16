using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEPBMmanager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArmyTrainStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<int>(
                name: "TrainSteel",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TrainTimber",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(
                name: "TrainSteel",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "TrainTimber",
                table: "Armies");
        }
    }
}
