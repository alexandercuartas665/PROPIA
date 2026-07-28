using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTableroCampoExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "campos_suma",
                table: "tablero_campos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "permite_varios",
                table: "tablero_campos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "requerido",
                table: "tablero_campos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "valor_por_defecto",
                table: "tablero_campos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "campos_suma",
                table: "tablero_campos");

            migrationBuilder.DropColumn(
                name: "permite_varios",
                table: "tablero_campos");

            migrationBuilder.DropColumn(
                name: "requerido",
                table: "tablero_campos");

            migrationBuilder.DropColumn(
                name: "valor_por_defecto",
                table: "tablero_campos");
        }
    }
}
