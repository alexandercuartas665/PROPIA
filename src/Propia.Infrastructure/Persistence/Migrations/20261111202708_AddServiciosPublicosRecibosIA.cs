using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiciosPublicosRecibosIA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "archivo_key",
                table: "registros_consumo_servicio",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "archivo_nombre",
                table: "registros_consumo_servicio",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_limite_pago",
                table: "registros_consumo_servicio",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "lectura_actual",
                table: "registros_consumo_servicio",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "lectura_anterior",
                table: "registros_consumo_servicio",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "origen",
                table: "registros_consumo_servicio",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "consumo_max",
                table: "cuentas_servicio_publico",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "consumo_min",
                table: "cuentas_servicio_publico",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "dia_pago",
                table: "cuentas_servicio_publico",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "periodicidad",
                table: "cuentas_servicio_publico",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "referencia_normalizada",
                table: "cuentas_servicio_publico",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_max",
                table: "cuentas_servicio_publico",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_min",
                table: "cuentas_servicio_publico",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cuenta_servicio_publico_id",
                table: "contratos_servicio",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "registros_consumo_conceptos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    registro_consumo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    concepto = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_consumo_conceptos", x => x.id);
                    table.ForeignKey(
                        name: "FK_registros_consumo_conceptos_registros_consumo_servicio_regi~",
                        column: x => x.registro_consumo_id,
                        principalTable: "registros_consumo_servicio",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "servicio_publico_campo_definiciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    opciones = table.Column<string>(type: "text", nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servicio_publico_campo_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "servicio_publico_campo_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_servicio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_servicio_publico_campo_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_servicio_publico_campo_valores_cuentas_servicio_publico_cue~",
                        column: x => x.cuenta_servicio_id,
                        principalTable: "cuentas_servicio_publico",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_servicio_publico_campo_valores_servicio_publico_campo_defin~",
                        column: x => x.definicion_id,
                        principalTable: "servicio_publico_campo_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill de referencia_normalizada (sin espacios, puntos ni guiones; mayusculas) ANTES del indice unico.
            // Duplicados dentro de una copropiedad: la cuenta mas antigua conserva la referencia y las demas quedan en
            // NULL (no rompe la migracion); se reportan con RAISE NOTICE para corregirlas a mano en Configuracion.
            // La tabla tiene RLS FORCE: el owner la suspende solo durante el backfill.
            migrationBuilder.Sql(@"
ALTER TABLE cuentas_servicio_publico NO FORCE ROW LEVEL SECURITY;
UPDATE cuentas_servicio_publico
   SET referencia_normalizada = NULLIF(upper(regexp_replace(coalesce(numero_cuenta, ''), '[\s.\-]', '', 'g')), '');
DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT id, tenant_id, alias, numero_cuenta, referencia_normalizada FROM (
            SELECT id, tenant_id, alias, numero_cuenta, referencia_normalizada,
                   row_number() OVER (PARTITION BY tenant_id, referencia_normalizada ORDER BY created_at, id) AS n
              FROM cuentas_servicio_publico WHERE referencia_normalizada IS NOT NULL) x
         WHERE x.n > 1
    LOOP
        RAISE NOTICE 'Servicios publicos: referencia duplicada % (tenant %, cuenta %, alias %): queda sin referencia normalizada',
            r.numero_cuenta, r.tenant_id, r.id, r.alias;
        UPDATE cuentas_servicio_publico SET referencia_normalizada = NULL WHERE id = r.id;
    END LOOP;
END $$;
ALTER TABLE cuentas_servicio_publico FORCE ROW LEVEL SECURITY;");

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_servicio_publico_tenant_id_referencia_normalizada",
                table: "cuentas_servicio_publico",
                columns: new[] { "tenant_id", "referencia_normalizada" },
                unique: true,
                filter: "referencia_normalizada IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_contratos_servicio_cuenta_servicio_publico_id",
                table: "contratos_servicio",
                column: "cuenta_servicio_publico_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_consumo_conceptos_registro_consumo_id",
                table: "registros_consumo_conceptos",
                column: "registro_consumo_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_consumo_conceptos_tenant_id",
                table: "registros_consumo_conceptos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_servicio_publico_campo_definiciones_tenant_id",
                table: "servicio_publico_campo_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_servicio_publico_campo_definiciones_tenant_id_label",
                table: "servicio_publico_campo_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_servicio_publico_campo_valores_cuenta_servicio_id",
                table: "servicio_publico_campo_valores",
                column: "cuenta_servicio_id");

            migrationBuilder.CreateIndex(
                name: "IX_servicio_publico_campo_valores_definicion_id_cuenta_servici~",
                table: "servicio_publico_campo_valores",
                columns: new[] { "definicion_id", "cuenta_servicio_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_servicio_publico_campo_valores_tenant_id",
                table: "servicio_publico_campo_valores",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_contratos_servicio_cuentas_servicio_publico_cuenta_servicio~",
                table: "contratos_servicio",
                column: "cuenta_servicio_publico_id",
                principalTable: "cuentas_servicio_publico",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // RLS FORCE + policy + GRANT en las tablas nuevas (RlsCoverageTests lo exige).
            foreach (var t in new[] { "registros_consumo_conceptos", "servicio_publico_campo_definiciones", "servicio_publico_campo_valores" })
            {
                migrationBuilder.Sql($"ALTER TABLE {t} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {t} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"
CREATE POLICY tenant_isolation ON {t}
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());");
                migrationBuilder.Sql($"GRANT SELECT, INSERT, UPDATE, DELETE ON {t} TO propia_app;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contratos_servicio_cuentas_servicio_publico_cuenta_servicio~",
                table: "contratos_servicio");

            migrationBuilder.DropTable(
                name: "registros_consumo_conceptos");

            migrationBuilder.DropTable(
                name: "servicio_publico_campo_valores");

            migrationBuilder.DropTable(
                name: "servicio_publico_campo_definiciones");

            migrationBuilder.DropIndex(
                name: "IX_cuentas_servicio_publico_tenant_id_referencia_normalizada",
                table: "cuentas_servicio_publico");

            migrationBuilder.DropIndex(
                name: "IX_contratos_servicio_cuenta_servicio_publico_id",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "archivo_key",
                table: "registros_consumo_servicio");

            migrationBuilder.DropColumn(
                name: "archivo_nombre",
                table: "registros_consumo_servicio");

            migrationBuilder.DropColumn(
                name: "fecha_limite_pago",
                table: "registros_consumo_servicio");

            migrationBuilder.DropColumn(
                name: "lectura_actual",
                table: "registros_consumo_servicio");

            migrationBuilder.DropColumn(
                name: "lectura_anterior",
                table: "registros_consumo_servicio");

            migrationBuilder.DropColumn(
                name: "origen",
                table: "registros_consumo_servicio");

            migrationBuilder.DropColumn(
                name: "consumo_max",
                table: "cuentas_servicio_publico");

            migrationBuilder.DropColumn(
                name: "consumo_min",
                table: "cuentas_servicio_publico");

            migrationBuilder.DropColumn(
                name: "dia_pago",
                table: "cuentas_servicio_publico");

            migrationBuilder.DropColumn(
                name: "periodicidad",
                table: "cuentas_servicio_publico");

            migrationBuilder.DropColumn(
                name: "referencia_normalizada",
                table: "cuentas_servicio_publico");

            migrationBuilder.DropColumn(
                name: "valor_max",
                table: "cuentas_servicio_publico");

            migrationBuilder.DropColumn(
                name: "valor_min",
                table: "cuentas_servicio_publico");

            migrationBuilder.DropColumn(
                name: "cuenta_servicio_publico_id",
                table: "contratos_servicio");
        }
    }
}
