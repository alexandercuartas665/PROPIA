using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoVisitanteClave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo_visitante_clave",
                table: "registros_visita",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_visitante_clave",
                table: "autorizaciones_previa",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tipo_visitante_clave",
                table: "registros_visita");

            migrationBuilder.DropColumn(
                name: "tipo_visitante_clave",
                table: "autorizaciones_previa");
        }
    }
}
