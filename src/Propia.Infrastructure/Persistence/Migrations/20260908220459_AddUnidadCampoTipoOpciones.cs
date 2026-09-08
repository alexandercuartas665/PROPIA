using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadCampoTipoOpciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "opciones",
                table: "unidad_campos_definiciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo",
                table: "unidad_campos_definiciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "opciones",
                table: "unidad_campos_definiciones");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "unidad_campos_definiciones");
        }
    }
}
