using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEtiquetaIconoColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "etiquetas_catalogo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "icono",
                table: "etiquetas_catalogo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "orden",
                table: "etiquetas_catalogo",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "color",
                table: "etiquetas_catalogo");

            migrationBuilder.DropColumn(
                name: "icono",
                table: "etiquetas_catalogo");

            migrationBuilder.DropColumn(
                name: "orden",
                table: "etiquetas_catalogo");
        }
    }
}
