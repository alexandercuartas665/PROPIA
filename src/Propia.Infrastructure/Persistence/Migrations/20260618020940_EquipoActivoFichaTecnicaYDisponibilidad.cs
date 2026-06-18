using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EquipoActivoFichaTecnicaYDisponibilidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cantidad",
                table: "equipos_activos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "es_reservable",
                table: "equipos_activos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_adquisicion",
                table: "equipos_activos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "numero_factura",
                table: "equipos_activos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proveedor",
                table: "equipos_activos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo",
                table: "equipos_activos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_adquisicion",
                table: "equipos_activos",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "vida_util_anios",
                table: "equipos_activos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ventanas_disponibilidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_entidad = table.Column<int>(type: "integer", nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dia_semana = table.Column<int>(type: "integer", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_fin = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ventanas_disponibilidad", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ventanas_disponibilidad_tenant_id_tipo_entidad_entidad_id",
                table: "ventanas_disponibilidad",
                columns: new[] { "tenant_id", "tipo_entidad", "entidad_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ventanas_disponibilidad");

            migrationBuilder.DropColumn(
                name: "cantidad",
                table: "equipos_activos");

            migrationBuilder.DropColumn(
                name: "es_reservable",
                table: "equipos_activos");

            migrationBuilder.DropColumn(
                name: "fecha_adquisicion",
                table: "equipos_activos");

            migrationBuilder.DropColumn(
                name: "numero_factura",
                table: "equipos_activos");

            migrationBuilder.DropColumn(
                name: "proveedor",
                table: "equipos_activos");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "equipos_activos");

            migrationBuilder.DropColumn(
                name: "valor_adquisicion",
                table: "equipos_activos");

            migrationBuilder.DropColumn(
                name: "vida_util_anios",
                table: "equipos_activos");
        }
    }
}
