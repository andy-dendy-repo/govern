using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Govern.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameLobbyLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_game_players_user_id",
                table: "game_players");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "activity_at",
                table: "games",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "ix_games_phase_activity_at",
                table: "games",
                columns: new[] { "phase", "activity_at" });

            migrationBuilder.CreateIndex(
                name: "ix_game_players_user_id_joined_at",
                table: "game_players",
                columns: new[] { "user_id", "joined_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_games_phase_activity_at",
                table: "games");

            migrationBuilder.DropIndex(
                name: "ix_game_players_user_id_joined_at",
                table: "game_players");

            migrationBuilder.DropColumn(
                name: "activity_at",
                table: "games");

            migrationBuilder.CreateIndex(
                name: "ix_game_players_user_id",
                table: "game_players",
                column: "user_id");
        }
    }
}
