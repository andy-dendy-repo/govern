using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Govern.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "map",
                table: "games",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "generated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "map",
                table: "games");
        }
    }
}
