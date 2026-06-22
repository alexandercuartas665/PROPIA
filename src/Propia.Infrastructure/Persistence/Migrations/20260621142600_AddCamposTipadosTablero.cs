using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCamposTipadosTablero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "columna",
                table: "tablero_campos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "descripcion",
                table: "tablero_campos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mostrar_en_filtro",
                table: "tablero_campos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "opciones",
                table: "tablero_campos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo",
                table: "tablero_campos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "columna",
                table: "tablero_campos");

            migrationBuilder.DropColumn(
                name: "descripcion",
                table: "tablero_campos");

            migrationBuilder.DropColumn(
                name: "mostrar_en_filtro",
                table: "tablero_campos");

            migrationBuilder.DropColumn(
                name: "opciones",
                table: "tablero_campos");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "tablero_campos");
        }
    }
}
