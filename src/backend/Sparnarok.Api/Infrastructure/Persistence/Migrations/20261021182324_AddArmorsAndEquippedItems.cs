using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sparnarok.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArmorsAndEquippedItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EquippedArmorId",
                table: "Creatures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EquippedShieldId",
                table: "Creatures",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Armors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RawArmorClass = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseArmorClass = table.Column<int>(type: "integer", nullable: false),
                    ShieldBonus = table.Column<int>(type: "integer", nullable: false),
                    AllowsDexterityModifier = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDexterityModifier = table.Column<int>(type: "integer", nullable: true),
                    StrengthRequirement = table.Column<int>(type: "integer", nullable: true),
                    StealthDisadvantage = table.Column<bool>(type: "boolean", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    Cost = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Armors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_EquippedArmorId",
                table: "Creatures",
                column: "EquippedArmorId");

            migrationBuilder.CreateIndex(
                name: "IX_Creatures_EquippedShieldId",
                table: "Creatures",
                column: "EquippedShieldId");

            migrationBuilder.CreateIndex(
                name: "IX_Armors_Name",
                table: "Armors",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Creatures_Armors_EquippedArmorId",
                table: "Creatures",
                column: "EquippedArmorId",
                principalTable: "Armors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Creatures_Armors_EquippedShieldId",
                table: "Creatures",
                column: "EquippedShieldId",
                principalTable: "Armors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Creatures_Armors_EquippedArmorId",
                table: "Creatures");

            migrationBuilder.DropForeignKey(
                name: "FK_Creatures_Armors_EquippedShieldId",
                table: "Creatures");

            migrationBuilder.DropTable(
                name: "Armors");

            migrationBuilder.DropIndex(
                name: "IX_Creatures_EquippedArmorId",
                table: "Creatures");

            migrationBuilder.DropIndex(
                name: "IX_Creatures_EquippedShieldId",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "EquippedArmorId",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "EquippedShieldId",
                table: "Creatures");
        }
    }
}
