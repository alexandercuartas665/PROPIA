using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuOverrideCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "href",
                table: "menu_overrides",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "icon",
                table: "menu_overrides",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_custom",
                table: "menu_overrides",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "node_type",
                table: "menu_overrides",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "href",
                table: "menu_overrides");

            migrationBuilder.DropColumn(
                name: "icon",
                table: "menu_overrides");

            migrationBuilder.DropColumn(
                name: "is_custom",
                table: "menu_overrides");

            migrationBuilder.DropColumn(
                name: "node_type",
                table: "menu_overrides");
        }
    }
}
