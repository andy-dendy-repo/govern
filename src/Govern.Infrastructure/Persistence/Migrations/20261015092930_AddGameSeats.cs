using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Govern.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "seats",
                table: "games",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<bool>(
                name: "is_out",
                table: "game_players",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "seats",
                table: "games");

            migrationBuilder.DropColumn(
                name: "is_out",
                table: "game_players");
        }
    }
}
