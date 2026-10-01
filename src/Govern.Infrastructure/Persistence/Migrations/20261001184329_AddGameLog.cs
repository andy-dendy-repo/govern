using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Govern.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "log_length",
                table: "game_players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "game_log_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    turn = table.Column<int>(type: "integer", nullable: false),
                    events = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_log_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_log_entries_game_players_player_id",
                        column: x => x.player_id,
                        principalTable: "game_players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_game_log_entries_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_log_entries_game_id",
                table: "game_log_entries",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_log_entries_player_id_sequence",
                table: "game_log_entries",
                columns: new[] { "player_id", "sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_log_entries");

            migrationBuilder.DropColumn(
                name: "log_length",
                table: "game_players");
        }
    }
}
