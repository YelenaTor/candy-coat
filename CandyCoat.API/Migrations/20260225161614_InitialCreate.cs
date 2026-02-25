using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandyCoat.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Earnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    PatronName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Earnings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GambaPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Rules = table.Column<string>(type: "text", nullable: false),
                    AnnounceMacro = table.Column<string>(type: "text", nullable: false),
                    DefaultMultiplier = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GambaPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatronNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatronName = table.Column<string>(type: "text", nullable: false),
                    AuthorRole = table.Column<string>(type: "text", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatronNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Patrons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    World = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    VisitCount = table.Column<int>(type: "integer", nullable: false),
                    TotalGilSpent = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    RpHooks = table.Column<string>(type: "text", nullable: false),
                    FavoriteDrink = table.Column<string>(type: "text", nullable: false),
                    Allergies = table.Column<string>(type: "text", nullable: false),
                    BlacklistReason = table.Column<string>(type: "text", nullable: false),
                    BlacklistDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BlacklistFlaggedBy = table.Column<string>(type: "text", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patrons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    OccupiedBy = table.Column<string>(type: "text", nullable: false),
                    PatronName = table.Column<string>(type: "text", nullable: false),
                    OccupiedSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceMenu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceMenu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterName = table.Column<string>(type: "text", nullable: false),
                    HomeWorld = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    IsDnd = table.Column<bool>(type: "boolean", nullable: false),
                    ShiftStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastHeartbeat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Earnings_CreatedAt",
                table: "Earnings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Earnings_VenueId",
                table: "Earnings",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_GambaPresets_VenueId",
                table: "GambaPresets",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_PatronNotes_CreatedAt",
                table: "PatronNotes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PatronNotes_VenueId",
                table: "PatronNotes",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Patrons_VenueId",
                table: "Patrons",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Patrons_VenueId_Name",
                table: "Patrons",
                columns: new[] { "VenueId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_VenueId",
                table: "Rooms",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceMenu_VenueId",
                table: "ServiceMenu",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_VenueId",
                table: "Staff",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_VenueId_CharacterName",
                table: "Staff",
                columns: new[] { "VenueId", "CharacterName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Earnings");

            migrationBuilder.DropTable(
                name: "GambaPresets");

            migrationBuilder.DropTable(
                name: "PatronNotes");

            migrationBuilder.DropTable(
                name: "Patrons");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "ServiceMenu");

            migrationBuilder.DropTable(
                name: "Staff");
        }
    }
}
