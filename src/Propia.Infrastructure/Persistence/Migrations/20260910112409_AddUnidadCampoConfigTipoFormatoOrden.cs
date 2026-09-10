using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadCampoConfigTipoFormatoOrden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "formato",
                table: "unidad_campos_config",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "oculto",
                table: "unidad_campos_config",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "orden",
                table: "unidad_campos_config",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo",
                table: "unidad_campos_config",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "formato",
                table: "unidad_campos_config");

            migrationBuilder.DropColumn(
                name: "oculto",
                table: "unidad_campos_config");

            migrationBuilder.DropColumn(
                name: "orden",
                table: "unidad_campos_config");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "unidad_campos_config");
        }
    }
}
