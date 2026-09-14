using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MEPBMmanager.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerTroopEquipmentAndNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Artifacts_Characters_CharacterId",
                table: "Artifacts");

            migrationBuilder.DropIndex(
                name: "IX_Artifacts_CharacterId",
                table: "Artifacts");

            migrationBuilder.DropColumn(
                name: "CharacterId",
                table: "Artifacts");

            migrationBuilder.RenameColumn(
                name: "WeaponRank",
                table: "Armies",
                newName: "MAAWeaponRank");

            migrationBuilder.RenameColumn(
                name: "ArmourRank",
                table: "Armies",
                newName: "MAAArmourRank");

            migrationBuilder.AddColumn<string>(
                name: "RoleId",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Players",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Players",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WantsToPlayWithUserId",
                table: "Players",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NavyId",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArcherArmourRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ArcherWeaponRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BorderTownFortification",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BorderTownName",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BorderTownSize",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CapitalFortification",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "CapitalHasHarbour",
                table: "NationTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CapitalHasPort",
                table: "NationTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CapitalName",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CapitalSize",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Character1Name",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Character2Name",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Character3Name",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Character4Name",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Character5Name",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Character6Name",
                table: "NationTemplates",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "FixedAllegiance",
                table: "NationTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HCArmourRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HCWeaponRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HIArmourRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HIWeaponRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LCArmourRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LCWeaponRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LIArmourRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LIWeaponRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MAAArmourRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MAAWeaponRank",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAdmin",
                table: "NationTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "StartingArchers",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingBronze",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingFood",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingGold",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingHeavyCavalry",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingHeavyInfantry",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingLeather",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingLightCavalry",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingLightInfantry",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingMenAtArms",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingMithril",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingMorale",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingMounts",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingSteel",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingTimber",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StartingTraining",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaxRate",
                table: "NationTemplates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasBridge",
                table: "HexTiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ArcherArmourRank",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ArcherWeaponRank",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HCArmourRank",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HCWeaponRank",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HIArmourRank",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HIWeaponRank",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LCArmourRank",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LCWeaponRank",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LIArmourRank",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LIWeaponRank",
                table: "Armies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Encounters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    GameId = table.Column<string>(type: "text", nullable: false),
                    LocationHex = table.Column<string>(type: "text", nullable: true),
                    CharacterId = table.Column<string>(type: "text", nullable: true),
                    ArmyId = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Encounters_Armies_ArmyId",
                        column: x => x.ArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Encounters_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Encounters_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameAdmins",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    GameId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    IsReady = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameAdmins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameAdmins_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameAdmins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_RoleId",
                table: "Users",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_WantsToPlayWithUserId",
                table: "Players",
                column: "WantsToPlayWithUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_NavyId",
                table: "Orders",
                column: "NavyId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_HeldByCharacterId",
                table: "Artifacts",
                column: "HeldByCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_ArmyId",
                table: "Encounters",
                column: "ArmyId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_CharacterId",
                table: "Encounters",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_GameId",
                table: "Encounters",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameAdmins_GameId_UserId",
                table: "GameAdmins",
                columns: new[] { "GameId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameAdmins_UserId",
                table: "GameAdmins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Artifacts_Characters_HeldByCharacterId",
                table: "Artifacts",
                column: "HeldByCharacterId",
                principalTable: "Characters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Navies_NavyId",
                table: "Orders",
                column: "NavyId",
                principalTable: "Navies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Users_WantsToPlayWithUserId",
                table: "Players",
                column: "WantsToPlayWithUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Artifacts_Characters_HeldByCharacterId",
                table: "Artifacts");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Navies_NavyId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Players_Users_WantsToPlayWithUserId",
                table: "Players");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_RoleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Encounters");

            migrationBuilder.DropTable(
                name: "GameAdmins");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Users_RoleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Players_WantsToPlayWithUserId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Orders_NavyId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Artifacts_HeldByCharacterId",
                table: "Artifacts");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "WantsToPlayWithUserId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "NavyId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ArcherArmourRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "ArcherWeaponRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "BorderTownFortification",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "BorderTownName",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "BorderTownSize",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "CapitalFortification",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "CapitalHasHarbour",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "CapitalHasPort",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "CapitalName",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "CapitalSize",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "Character1Name",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "Character2Name",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "Character3Name",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "Character4Name",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "Character5Name",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "Character6Name",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "FixedAllegiance",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "HCArmourRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "HCWeaponRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "HIArmourRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "HIWeaponRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "LCArmourRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "LCWeaponRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "LIArmourRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "LIWeaponRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "MAAArmourRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "MAAWeaponRank",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "RequiresAdmin",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingArchers",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingBronze",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingFood",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingGold",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingHeavyCavalry",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingHeavyInfantry",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingLeather",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingLightCavalry",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingLightInfantry",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingMenAtArms",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingMithril",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingMorale",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingMounts",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingSteel",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingTimber",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "StartingTraining",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "NationTemplates");

            migrationBuilder.DropColumn(
                name: "HasBridge",
                table: "HexTiles");

            migrationBuilder.DropColumn(
                name: "ArcherArmourRank",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "ArcherWeaponRank",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "HCArmourRank",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "HCWeaponRank",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "HIArmourRank",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "HIWeaponRank",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "LCArmourRank",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "LCWeaponRank",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "LIArmourRank",
                table: "Armies");

            migrationBuilder.DropColumn(
                name: "LIWeaponRank",
                table: "Armies");

            migrationBuilder.RenameColumn(
                name: "MAAWeaponRank",
                table: "Armies",
                newName: "WeaponRank");

            migrationBuilder.RenameColumn(
                name: "MAAArmourRank",
                table: "Armies",
                newName: "ArmourRank");

            migrationBuilder.AddColumn<string>(
                name: "CharacterId",
                table: "Artifacts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_CharacterId",
                table: "Artifacts",
                column: "CharacterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Artifacts_Characters_CharacterId",
                table: "Artifacts",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id");
        }
    }
}
