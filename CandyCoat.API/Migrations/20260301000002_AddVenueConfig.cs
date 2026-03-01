using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandyCoat.API.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VenueConfig",
                columns: table => new
                {
                    VenueId = table.Column<string>(type: "text", nullable: false),
                    ManagerPwAdded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ManagerPw = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueConfig", x => x.VenueId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VenueConfig");
        }
    }
}
