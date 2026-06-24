using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiciosPublicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cuentas_servicio_publico",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    alias = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    prestador = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    numero_cuenta = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    metodo_pago = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    unidad_medida = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    umbral_alerta_pct = table.Column<int>(type: "integer", nullable: false, defaultValue: 25),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuentas_servicio_publico", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reclamaciones_servicio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motivo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    radicado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reclamaciones_servicio", x => x.id);
                    table.ForeignKey(
                        name: "FK_reclamaciones_servicio_cuentas_servicio_publico_cuenta_serv~",
                        column: x => x.cuenta_servicio_id,
                        principalTable: "cuentas_servicio_publico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registros_consumo_servicio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    consumo = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    nota_admin = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_consumo_servicio", x => x.id);
                    table.ForeignKey(
                        name: "FK_registros_consumo_servicio_cuentas_servicio_publico_cuenta_~",
                        column: x => x.cuenta_servicio_id,
                        principalTable: "cuentas_servicio_publico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_servicio_publico_tenant_id",
                table: "cuentas_servicio_publico",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_reclamaciones_servicio_cuenta_servicio_id",
                table: "reclamaciones_servicio",
                column: "cuenta_servicio_id");

            migrationBuilder.CreateIndex(
                name: "IX_reclamaciones_servicio_tenant_id",
                table: "reclamaciones_servicio",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_consumo_servicio_cuenta_servicio_id_anio_mes",
                table: "registros_consumo_servicio",
                columns: new[] { "cuenta_servicio_id", "anio", "mes" });

            migrationBuilder.CreateIndex(
                name: "IX_registros_consumo_servicio_tenant_id",
                table: "registros_consumo_servicio",
                column: "tenant_id");

            // RLS por tenant (mismo patron que el resto del modelo).
            foreach (var tabla in new[] { "cuentas_servicio_publico", "registros_consumo_servicio", "reclamaciones_servicio" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {tabla}
                        USING (tenant_id = current_tenant_id())
                        WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {tabla} TO propia_app;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reclamaciones_servicio");

            migrationBuilder.DropTable(
                name: "registros_consumo_servicio");

            migrationBuilder.DropTable(
                name: "cuentas_servicio_publico");
        }
    }
}
