using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EquipoFichaTecnicaRica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipo_contrato_vinculos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipo_contrato_vinculos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipo_fotos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipo_fotos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipo_mejoras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    documento_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipo_mejoras", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipo_vinculos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_vinculado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipo_vinculos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_equipo_contrato_vinculos_tenant_id_equipo_activo_id",
                table: "equipo_contrato_vinculos",
                columns: new[] { "tenant_id", "equipo_activo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_equipo_fotos_tenant_id_equipo_activo_id",
                table: "equipo_fotos",
                columns: new[] { "tenant_id", "equipo_activo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_equipo_mejoras_tenant_id_equipo_activo_id",
                table: "equipo_mejoras",
                columns: new[] { "tenant_id", "equipo_activo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_equipo_vinculos_tenant_id_equipo_activo_id",
                table: "equipo_vinculos",
                columns: new[] { "tenant_id", "equipo_activo_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipo_contrato_vinculos");

            migrationBuilder.DropTable(
                name: "equipo_fotos");

            migrationBuilder.DropTable(
                name: "equipo_mejoras");

            migrationBuilder.DropTable(
                name: "equipo_vinculos");
        }
    }
}
