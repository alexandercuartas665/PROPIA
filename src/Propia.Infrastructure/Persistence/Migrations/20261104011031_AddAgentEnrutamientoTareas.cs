using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentEnrutamientoTareas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "allowed_board_ids_json",
                table: "ai_agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "assignee_cursor",
                table: "ai_agents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "assignee_user_ids_json",
                table: "ai_agents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allowed_board_ids_json",
                table: "ai_agents");

            migrationBuilder.DropColumn(
                name: "assignee_cursor",
                table: "ai_agents");

            migrationBuilder.DropColumn(
                name: "assignee_user_ids_json",
                table: "ai_agents");
        }
    }
}
