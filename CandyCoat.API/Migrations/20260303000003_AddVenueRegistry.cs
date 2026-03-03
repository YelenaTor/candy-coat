using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandyCoat.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueKey = table.Column<string>(type: "text", nullable: false),
                    VenueName = table.Column<string>(type: "text", nullable: false),
                    OwnerProfileId = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Venues_VenueKey",
                table: "Venues",
                column: "VenueKey",
                unique: true);

            // Seed Sugar venue — Id is deterministic: new Guid(MD5("sugar-venue-2026-master-13"))
            // This matches PluginConstants.SugarVenueId so existing Sugar data keeps its VenueId FK.
            migrationBuilder.InsertData(
                table: "Venues",
                columns: new[] { "Id", "VenueKey", "VenueName", "OwnerProfileId", "CreatedAt", "IsActive" },
                values: new object[]
                {
                    new Guid("7c303cf0-a169-49c3-186f-8bc93d58616c"),
                    "sugar-venue-2026-master-13",
                    "Sugar",
                    "",
                    new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc),
                    true
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Venues");
        }
    }
}
