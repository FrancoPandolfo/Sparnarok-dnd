using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Sparnarok.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Creatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Size = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Alignment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChallengeRating = table.Column<double>(type: "double precision", nullable: false),
                    SpeedFeet = table.Column<int>(type: "integer", nullable: false),
                    Strength = table.Column<int>(type: "integer", nullable: false),
                    Dexterity = table.Column<int>(type: "integer", nullable: false),
                    Constitution = table.Column<int>(type: "integer", nullable: false),
                    Intelligence = table.Column<int>(type: "integer", nullable: false),
                    Wisdom = table.Column<int>(type: "integer", nullable: false),
                    Charisma = table.Column<int>(type: "integer", nullable: false),
                    MaxHitPoints = table.Column<int>(type: "integer", nullable: false),
                    CurrentHitPoints = table.Column<int>(type: "integer", nullable: false),
                    TemporaryHitPoints = table.Column<int>(type: "integer", nullable: false),
                    ArmorClassBase = table.Column<int>(type: "integer", nullable: false),
                    ArmorClassShieldBonus = table.Column<int>(type: "integer", nullable: false),
                    ArmorClassAllowsDex = table.Column<bool>(type: "boolean", nullable: false),
                    ArmorClassMaxDex = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Creatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeaponMasteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Property = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PrerequisiteWeapon = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeaponMasteries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsAttack = table.Column<bool>(type: "boolean", nullable: false),
                    AttackModifierAbility = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReachOrRangeFeet = table.Column<int>(type: "integer", nullable: false),
                    DamageFormula = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DamageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AssociatedMasteryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Actions_Creatures_CreatureId",
                        column: x => x.CreatureId,
                        principalTable: "Creatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Actions_WeaponMasteries_AssociatedMasteryId",
                        column: x => x.AssociatedMasteryId,
                        principalTable: "WeaponMasteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CreatureWeaponMasteries",
                columns: table => new
                {
                    CreatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeaponMasteryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureWeaponMasteries", x => new { x.CreatureId, x.WeaponMasteryId });
                    table.ForeignKey(
                        name: "FK_CreatureWeaponMasteries_Creatures_CreatureId",
                        column: x => x.CreatureId,
                        principalTable: "Creatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureWeaponMasteries_WeaponMasteries_WeaponMasteryId",
                        column: x => x.WeaponMasteryId,
                        principalTable: "WeaponMasteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "WeaponMasteries",
                columns: new[] { "Id", "Description", "Name", "PrerequisiteWeapon", "Property" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "Si impactas a una criatura con un arma cuerpo a cuerpo pesada, puedes hacer una tirada de ataque contra una segunda criatura a 5 pies.", "Cleave", "Melee, Heavy", "Cleave" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "Si fallas una tirada de ataque, infliges daño igual al modificador de la característica usada para el ataque.", "Graze", "Melee, Heavy", "Graze" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "Permite realizar el ataque adicional de la propiedad Ligera como parte de la acción de Ataque en lugar de una Acción Adicional.", "Nick", "Light", "Nick" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "Si impactas a una criatura Grande o menor, puedes empujarla hasta 10 pies en línea recta.", "Push", "Heavy, Two-Handed or Versatile", "Push" },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "Si impactas a una criatura, esta tiene desventaja en su próxima tirada de ataque antes del inicio de tu siguiente turno.", "Sap", "Versatile or Melee", "Sap" },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "Si impactas e infliges daño, reduces la velocidad del objetivo en 10 pies hasta el inicio de tu próximo turno.", "Slow", "Ranged or Melee", "Slow" },
                    { new Guid("00000000-0000-0000-0000-000000000007"), "Si impactas a una criatura, esta debe superar una salvación de Constitución (CD 8 + Modificador + Bono Competencia) o quedar Derribada.", "Topple", "Heavy or Versatile", "Topple" },
                    { new Guid("00000000-0000-0000-0000-000000000008"), "Si impactas e infliges daño, obtienes ventaja en tu próxima tirada de ataque contra ese objetivo antes del final de tu siguiente turno.", "Vex", "Ammunition, Finesse or Light", "Vex" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Actions_AssociatedMasteryId",
                table: "Actions",
                column: "AssociatedMasteryId");

            migrationBuilder.CreateIndex(
                name: "IX_Actions_CreatureId",
                table: "Actions",
                column: "CreatureId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureWeaponMasteries_WeaponMasteryId",
                table: "CreatureWeaponMasteries",
                column: "WeaponMasteryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Actions");

            migrationBuilder.DropTable(
                name: "CreatureWeaponMasteries");

            migrationBuilder.DropTable(
                name: "Creatures");

            migrationBuilder.DropTable(
                name: "WeaponMasteries");
        }
    }
}
