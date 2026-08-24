using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeguros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "poliza_campo_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poliza_id = table.Column<Guid>(type: "uuid", nullable: false),
                    poliza_campo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poliza_campo_valores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "poliza_campos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    opciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poliza_campos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "polizas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_poliza = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    aseguradora_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aseguradora_empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aseguradora = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    corredor_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corredor_empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    corredor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    valor_poliza = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    forma_pago_cuotas = table.Column<int>(type: "integer", nullable: true),
                    pago_mensual = table.Column<bool>(type: "boolean", nullable: false),
                    cobertura = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    incluye_zonas_unidades = table.Column<bool>(type: "boolean", nullable: false),
                    valores_agregados = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    expediente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    alerta_vencimiento_pct_notificado = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polizas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "poliza_reclamaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poliza_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    monto_reclamado = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    monto_reconocido = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    fecha_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expediente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poliza_reclamaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_poliza_reclamaciones_polizas_poliza_id",
                        column: x => x.poliza_id,
                        principalTable: "polizas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contrato_expedientes_contrato_id_expediente_id",
                table: "contrato_expedientes",
                columns: new[] { "contrato_id", "expediente_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contrato_expedientes_tenant_id_contrato_id",
                table: "contrato_expedientes",
                columns: new[] { "tenant_id", "contrato_id" });

            migrationBuilder.CreateIndex(
                name: "IX_poliza_campo_valores_poliza_id_poliza_campo_id",
                table: "poliza_campo_valores",
                columns: new[] { "poliza_id", "poliza_campo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_poliza_campo_valores_tenant_id_poliza_id",
                table: "poliza_campo_valores",
                columns: new[] { "tenant_id", "poliza_id" });

            migrationBuilder.CreateIndex(
                name: "IX_poliza_campos_tenant_id",
                table: "poliza_campos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_poliza_reclamaciones_poliza_id",
                table: "poliza_reclamaciones",
                column: "poliza_id");

            migrationBuilder.CreateIndex(
                name: "IX_poliza_reclamaciones_tenant_id_poliza_id",
                table: "poliza_reclamaciones",
                columns: new[] { "tenant_id", "poliza_id" });

            migrationBuilder.CreateIndex(
                name: "IX_polizas_fecha_fin",
                table: "polizas",
                column: "fecha_fin");

            migrationBuilder.CreateIndex(
                name: "IX_polizas_tenant_id",
                table: "polizas",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "poliza_campo_valores");

            migrationBuilder.DropTable(
                name: "poliza_campos");

            migrationBuilder.DropTable(
                name: "poliza_reclamaciones");

            migrationBuilder.DropTable(
                name: "polizas");

            migrationBuilder.DropIndex(
                name: "IX_contrato_expedientes_contrato_id_expediente_id",
                table: "contrato_expedientes");

            migrationBuilder.DropIndex(
                name: "IX_contrato_expedientes_tenant_id_contrato_id",
                table: "contrato_expedientes");
        }
    }
}
