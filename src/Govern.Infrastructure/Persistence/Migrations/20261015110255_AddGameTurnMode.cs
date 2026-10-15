using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Govern.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameTurnMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "turn_mode",
                table: "games",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Sequential");

            migrationBuilder.AddColumn<bool>(
                name: "ended_turn",
                table: "game_players",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "turn_mode",
                table: "games");

            migrationBuilder.DropColumn(
                name: "ended_turn",
                table: "game_players");
        }
    }
}
