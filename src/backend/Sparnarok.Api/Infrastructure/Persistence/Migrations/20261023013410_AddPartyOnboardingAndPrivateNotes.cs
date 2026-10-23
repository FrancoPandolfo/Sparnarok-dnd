using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sparnarok.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyOnboardingAndPrivateNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GlobalNickname",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "SessionNotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "InviteToken",
                table: "Parties",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "PartyMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InGameName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartyMembers_Parties_PartyId",
                        column: x => x.PartyId,
                        principalTable: "Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartyMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionNotes_OwnerUserId",
                table: "SessionNotes",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionNotes_PartyId_OwnerUserId",
                table: "SessionNotes",
                columns: new[] { "PartyId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_InviteToken",
                table: "Parties",
                column: "InviteToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyMembers_PartyId_UserId",
                table: "PartyMembers",
                columns: new[] { "PartyId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartyMembers_UserId",
                table: "PartyMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionNotes_Users_OwnerUserId",
                table: "SessionNotes",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionNotes_Users_OwnerUserId",
                table: "SessionNotes");

            migrationBuilder.DropTable(
                name: "PartyMembers");

            migrationBuilder.DropIndex(
                name: "IX_SessionNotes_OwnerUserId",
                table: "SessionNotes");

            migrationBuilder.DropIndex(
                name: "IX_SessionNotes_PartyId_OwnerUserId",
                table: "SessionNotes");

            migrationBuilder.DropIndex(
                name: "IX_Parties_InviteToken",
                table: "Parties");

            migrationBuilder.DropColumn(
                name: "GlobalNickname",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "SessionNotes");

            migrationBuilder.DropColumn(
                name: "InviteToken",
                table: "Parties");
        }
    }
}
