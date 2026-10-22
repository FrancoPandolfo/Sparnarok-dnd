using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sparnarok.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyFundsCraftingAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CopperPieces",
                table: "Parties",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ElectrumPieces",
                table: "Parties",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GoldPieces",
                table: "Parties",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatinumPieces",
                table: "Parties",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SilverPieces",
                table: "Parties",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "CraftingQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rarity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GoldCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DaysRequired = table.Column<int>(type: "integer", nullable: false),
                    DaysRemaining = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsOfficialSrd = table.Column<bool>(type: "boolean", nullable: false),
                    ReferenceIndex = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CrafterName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraftingQueueItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraftingQueueItems_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StashActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserDisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StashActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StashActivityLogs_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CraftingQueueItems_PartyId",
                table: "CraftingQueueItems",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingQueueItems_PartyId_Status",
                table: "CraftingQueueItems",
                columns: new[] { "PartyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StashActivityLogs_PartyId",
                table: "StashActivityLogs",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_StashActivityLogs_PartyId_TimestampUtc",
                table: "StashActivityLogs",
                columns: new[] { "PartyId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CraftingQueueItems");

            migrationBuilder.DropTable(
                name: "StashActivityLogs");

            migrationBuilder.DropColumn(
                name: "CopperPieces",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "ElectrumPieces",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "GoldPieces",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "PlatinumPieces",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "SilverPieces",
                table: "Parties");
        }
    }
}
