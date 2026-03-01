using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CandyCoat.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalProfileIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasGlamourerIntegrated",
                table: "GlobalProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasChatTwoIntegrated",
                table: "GlobalProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasGlamourerIntegrated",
                table: "GlobalProfiles");

            migrationBuilder.DropColumn(
                name: "HasChatTwoIntegrated",
                table: "GlobalProfiles");
        }
    }
}
