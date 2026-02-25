using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandyCoat.API.Migrations
{
    /// <inheritdoc />
    public partial class CosmeticDrawer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CosmeticsSync",
                columns: table => new
                {
                    CharacterHash = table.Column<string>(type: "text", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrotliBlob = table.Column<byte[]>(type: "bytea", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CosmeticsSync", x => x.CharacterHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CosmeticsSync_VenueId",
                table: "CosmeticsSync",
                column: "VenueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CosmeticsSync");
        }
    }
}
