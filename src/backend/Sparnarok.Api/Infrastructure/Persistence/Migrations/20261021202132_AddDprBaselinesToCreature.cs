using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sparnarok.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDprBaselinesToCreature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Archetype",
                table: "Creatures",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ExpectedDpr",
                table: "Creatures",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Creatures",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Archetype",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "ExpectedDpr",
                table: "Creatures");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Creatures");
        }
    }
}
