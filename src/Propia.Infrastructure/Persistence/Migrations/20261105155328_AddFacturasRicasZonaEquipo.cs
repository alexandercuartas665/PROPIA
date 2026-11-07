using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFacturasRicasZonaEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "concepto",
                table: "zona_facturas",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "numero_factura",
                table: "zona_facturas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "observacion",
                table: "zona_facturas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "proveedor",
                table: "zona_facturas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "zona_documento_id",
                table: "zona_facturas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "equipo_facturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proveedor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    numero_factura = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    concepto = table.Column<string>(type: "text", nullable: true),
                    valor = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    equipo_documento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipo_facturas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_zona_facturas_tenant_id_zona_comun_id",
                table: "zona_facturas",
                columns: new[] { "tenant_id", "zona_comun_id" });

            migrationBuilder.CreateIndex(
                name: "IX_equipo_facturas_tenant_id_equipo_activo_id",
                table: "equipo_facturas",
                columns: new[] { "tenant_id", "equipo_activo_id" });

            // RLS: aislar las facturas de equipo por tenant (FORCE + policy + GRANT), como el resto de
            // tablas de tenant. RlsCoverageTests exige RLS en toda tabla con tenant_id. (zona_facturas ya
            // tiene RLS desde su creacion; aqui solo se cubre la tabla nueva.)
            migrationBuilder.Sql(@"
                ALTER TABLE equipo_facturas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE equipo_facturas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON equipo_facturas
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON equipo_facturas TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipo_facturas");

            migrationBuilder.DropIndex(
                name: "IX_zona_facturas_tenant_id_zona_comun_id",
                table: "zona_facturas");

            migrationBuilder.DropColumn(
                name: "numero_factura",
                table: "zona_facturas");

            migrationBuilder.DropColumn(
                name: "observacion",
                table: "zona_facturas");

            migrationBuilder.DropColumn(
                name: "proveedor",
                table: "zona_facturas");

            migrationBuilder.DropColumn(
                name: "zona_documento_id",
                table: "zona_facturas");

            migrationBuilder.AlterColumn<string>(
                name: "concepto",
                table: "zona_facturas",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
