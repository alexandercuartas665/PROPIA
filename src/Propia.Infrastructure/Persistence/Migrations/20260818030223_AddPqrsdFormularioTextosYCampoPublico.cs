using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdFormularioTextosYCampoPublico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "encabezado_texto",
                table: "pqrsd_formulario_publico_configs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pie_texto",
                table: "pqrsd_formulario_publico_configs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mostrar_en_publico",
                table: "pqrsd_campos",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "encabezado_texto",
                table: "pqrsd_formulario_publico_configs");

            migrationBuilder.DropColumn(
                name: "pie_texto",
                table: "pqrsd_formulario_publico_configs");

            migrationBuilder.DropColumn(
                name: "mostrar_en_publico",
                table: "pqrsd_campos");
        }
    }
}
