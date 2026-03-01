using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandyCoat.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalProfiles",
                columns: table => new
                {
                    ProfileId = table.Column<string>(type: "text", nullable: false),
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    HomeWorld = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    StaffData = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    PatronData = table.Column<string>(type: "text", nullable: false, defaultValue: "{}"),
                    RegisteredVenues = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalProfiles", x => x.ProfileId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalProfiles_CharacterName",
                table: "GlobalProfiles",
                column: "CharacterName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GlobalProfiles");
        }
    }
}
