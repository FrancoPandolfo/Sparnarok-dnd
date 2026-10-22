using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sparnarok.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyStashAndItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StashItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Weight = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false, defaultValue: 0m),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Rarity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Common"),
                    ItemType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "Adventuring Gear"),
                    IsOfficialSrd = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReferenceIndex = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AssignedCreatureId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StashItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StashItems_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StashItems_PartyId",
                table: "StashItems",
                column: "PartyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StashItems");
        }
    }
}
