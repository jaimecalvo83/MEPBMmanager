using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEPBMmanager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArmyTypeTraining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArcherTraining",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HCTraining",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HITraining",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LCTraining",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LITraining",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MAATraining",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArcherTraining",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "HCTraining",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "HITraining",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "LCTraining",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "LITraining",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "MAATraining",
                table: "Armies");
        }
    }
}
