using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEPBMmanager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameTypeAndScenario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Module",
                table: "Games");

            migrationBuilder.AddColumn<string>(
                name: "StartHex",
                table: "Nations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameTypeId",
                table: "HexTiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameTypeId",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NationTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    GameTypeId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Allegiance = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    StartHex = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NationTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NationTemplates_GameTypes_GameTypeId",
                        column: x => x.GameTypeId,
                        principalTable: "GameTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HexTiles_GameTypeId",
                table: "HexTiles",
                column: "GameTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_GameTypeId",
                table: "Games",
                column: "GameTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTypes_Code",
                table: "GameTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NationTemplates_GameTypeId_Name",
                table: "NationTemplates",
                columns: new[] { "GameTypeId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Games_GameTypes_GameTypeId",
                table: "Games",
                column: "GameTypeId",
                principalTable: "GameTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_HexTiles_GameTypes_GameTypeId",
                table: "HexTiles",
                column: "GameTypeId",
                principalTable: "GameTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_GameTypes_GameTypeId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_HexTiles_GameTypes_GameTypeId",
                table: "HexTiles");

            migrationBuilder.DropTable(
                name: "NationTemplates");

            migrationBuilder.DropTable(
                name: "GameTypes");

            migrationBuilder.DropIndex(
                name: "IX_HexTiles_GameTypeId",
                table: "HexTiles");

            migrationBuilder.DropIndex(
                name: "IX_Games_GameTypeId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "StartHex",
                table: "Nations");

            migrationBuilder.DropColumn(
                name: "GameTypeId",
                table: "HexTiles");

            migrationBuilder.DropColumn(
                name: "GameTypeId",
                table: "Games");

            migrationBuilder.AddColumn<string>(
                name: "Module",
                table: "Games",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
