using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CondicionesArchivoZonaEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "condiciones_archivo_nombre",
                table: "zonas_comunes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "condiciones_archivo_url",
                table: "zonas_comunes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "condiciones_archivo_nombre",
                table: "equipos_activos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "condiciones_archivo_url",
                table: "equipos_activos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "condiciones_archivo_nombre",
                table: "zonas_comunes");

            migrationBuilder.DropColumn(
                name: "condiciones_archivo_url",
                table: "zonas_comunes");

            migrationBuilder.DropColumn(
                name: "condiciones_archivo_nombre",
                table: "equipos_activos");

            migrationBuilder.DropColumn(
                name: "condiciones_archivo_url",
                table: "equipos_activos");
        }
    }
}
