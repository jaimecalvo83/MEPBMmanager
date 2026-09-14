using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEPBMmanager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNationVictoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VictoryPoints",
                table: "Nations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WarshipStrength",
                table: "Nations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VictoryPoints",
                table: "Nations");

            migrationBuilder.DropColumn(
                name: "WarshipStrength",
                table: "Nations");
        }
    }
}
