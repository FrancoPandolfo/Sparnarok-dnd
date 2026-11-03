using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sparnarok.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyContextToEncounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PartyId",
                table: "Encounters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_PartyId",
                table: "Encounters",
                column: "PartyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Encounters_Parties_PartyId",
                table: "Encounters",
                column: "PartyId",
                principalTable: "Parties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Encounters_Parties_PartyId",
                table: "Encounters");

            migrationBuilder.DropIndex(
                name: "IX_Encounters_PartyId",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "PartyId",
                table: "Encounters");
        }
    }
}
