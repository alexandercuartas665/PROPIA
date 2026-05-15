using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPresupuestoModulo26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log_presupuestos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    valor_anterior = table.Column<string>(type: "jsonb", nullable: true),
                    valor_nuevo = table.Column<string>(type: "jsonb", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log_presupuestos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cuotas_extraordinarias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    proposito = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    monto_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    forma_recaudo = table.Column<int>(type: "integer", nullable: false),
                    numero_cuotas = table.Column<int>(type: "integer", nullable: true),
                    base_liquidacion = table.Column<int>(type: "integer", nullable: false),
                    proyecto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    aprobacion_tipo = table.Column<int>(type: "integer", nullable: true),
                    aprobacion_acta_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    asamblea_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_inicio_recaudo = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_fin_recaudo = table.Column<DateOnly>(type: "date", nullable: true),
                    creada_por_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuotas_extraordinarias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "presupuestos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fin = table.Column<DateOnly>(type: "date", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    monto_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    aprobacion_tipo = table.Column<int>(type: "integer", nullable: true),
                    aprobacion_acta_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aprobacion_fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    aprobacion_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asamblea_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_presupuestos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "liquidaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    presupuesto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo = table.Column<DateOnly>(type: "date", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    monto_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    snapshot_calculo = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    emitida_por = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liquidaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_liquidaciones_presupuestos_presupuesto_id",
                        column: x => x.presupuesto_id,
                        principalTable: "presupuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "presupuesto_rubros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    presupuesto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    monto_anual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    base_liquidacion = table.Column<int>(type: "integer", nullable: false),
                    es_fondo_imprevistos = table.Column<bool>(type: "boolean", nullable: false),
                    es_obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    notas_internas = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_presupuesto_rubros", x => x.id);
                    table.ForeignKey(
                        name: "FK_presupuesto_rubros_presupuestos_presupuesto_id",
                        column: x => x.presupuesto_id,
                        principalTable: "presupuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "liquidacion_unidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    liquidacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    desglose = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    estado_pago = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liquidacion_unidades", x => x.id);
                    table.ForeignKey(
                        name: "FK_liquidacion_unidades_liquidaciones_liquidacion_id",
                        column: x => x.liquidacion_id,
                        principalTable: "liquidaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_liquidacion_unidades_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ejecuciones_presupuestales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    presupuesto_rubro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    soporte_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registrado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ejecuciones_presupuestales", x => x.id);
                    table.ForeignKey(
                        name: "FK_ejecuciones_presupuestales_presupuesto_rubros_presupuesto_r~",
                        column: x => x.presupuesto_rubro_id,
                        principalTable: "presupuesto_rubros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pagos_cuotas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    liquidacion_unidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuota_extraordinaria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    canal = table.Column<int>(type: "integer", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    referencia_externa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fecha_pago = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    es_manual = table.Column<bool>(type: "boolean", nullable: false),
                    registrado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    notas = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos_cuotas", x => x.id);
                    table.ForeignKey(
                        name: "FK_pagos_cuotas_cuotas_extraordinarias_cuota_extraordinaria_id",
                        column: x => x.cuota_extraordinaria_id,
                        principalTable: "cuotas_extraordinarias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pagos_cuotas_liquidacion_unidades_liquidacion_unidad_id",
                        column: x => x.liquidacion_unidad_id,
                        principalTable: "liquidacion_unidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_pagos_cuotas_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_presupuestos_created_at",
                table: "audit_log_presupuestos",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_presupuestos_entidad_entidad_id",
                table: "audit_log_presupuestos",
                columns: new[] { "entidad", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_presupuestos_tenant_id",
                table: "audit_log_presupuestos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_cuotas_extraordinarias_tenant_id",
                table: "cuotas_extraordinarias",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_cuotas_extraordinarias_tenant_id_estado",
                table: "cuotas_extraordinarias",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_ejecuciones_presupuestales_presupuesto_rubro_id",
                table: "ejecuciones_presupuestales",
                column: "presupuesto_rubro_id");

            migrationBuilder.CreateIndex(
                name: "IX_ejecuciones_presupuestales_tenant_id",
                table: "ejecuciones_presupuestales",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_liquidacion_unidades_estado_pago",
                table: "liquidacion_unidades",
                column: "estado_pago");

            migrationBuilder.CreateIndex(
                name: "IX_liquidacion_unidades_liquidacion_id_unidad_privada_id",
                table: "liquidacion_unidades",
                columns: new[] { "liquidacion_id", "unidad_privada_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_liquidacion_unidades_tenant_id",
                table: "liquidacion_unidades",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_liquidacion_unidades_unidad_privada_id",
                table: "liquidacion_unidades",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_presupuesto_id_periodo",
                table: "liquidaciones",
                columns: new[] { "presupuesto_id", "periodo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_liquidaciones_tenant_id",
                table: "liquidaciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_cuotas_cuota_extraordinaria_id",
                table: "pagos_cuotas",
                column: "cuota_extraordinaria_id");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_cuotas_liquidacion_unidad_id",
                table: "pagos_cuotas",
                column: "liquidacion_unidad_id");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_cuotas_referencia_externa",
                table: "pagos_cuotas",
                column: "referencia_externa");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_cuotas_tenant_id",
                table: "pagos_cuotas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_cuotas_tenant_id_estado",
                table: "pagos_cuotas",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_pagos_cuotas_unidad_privada_id",
                table: "pagos_cuotas",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_presupuesto_rubros_presupuesto_id_codigo",
                table: "presupuesto_rubros",
                columns: new[] { "presupuesto_id", "codigo" });

            migrationBuilder.CreateIndex(
                name: "IX_presupuesto_rubros_tenant_id",
                table: "presupuesto_rubros",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_presupuestos_tenant_id",
                table: "presupuestos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_presupuestos_tenant_id_estado",
                table: "presupuestos",
                columns: new[] { "tenant_id", "estado" });

            // RLS + grants para las 8 tablas del modulo 2.6
            migrationBuilder.Sql(@"
                ALTER TABLE presupuestos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE presupuestos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON presupuestos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON presupuestos TO propia_app;

                ALTER TABLE presupuesto_rubros ENABLE ROW LEVEL SECURITY;
                ALTER TABLE presupuesto_rubros FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON presupuesto_rubros
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON presupuesto_rubros TO propia_app;

                ALTER TABLE liquidaciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE liquidaciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON liquidaciones
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON liquidaciones TO propia_app;

                ALTER TABLE liquidacion_unidades ENABLE ROW LEVEL SECURITY;
                ALTER TABLE liquidacion_unidades FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON liquidacion_unidades
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON liquidacion_unidades TO propia_app;

                ALTER TABLE pagos_cuotas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pagos_cuotas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pagos_cuotas
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pagos_cuotas TO propia_app;

                ALTER TABLE cuotas_extraordinarias ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cuotas_extraordinarias FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON cuotas_extraordinarias
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON cuotas_extraordinarias TO propia_app;

                ALTER TABLE ejecuciones_presupuestales ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ejecuciones_presupuestales FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON ejecuciones_presupuestales
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON ejecuciones_presupuestales TO propia_app;

                ALTER TABLE audit_log_presupuestos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE audit_log_presupuestos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON audit_log_presupuestos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT ON audit_log_presupuestos TO propia_app;
            ");

            // Trigger append-only en audit_log_presupuestos (RN-12 + RN-14)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION audit_log_presupuesto_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_log_presupuestos es append-only: % no permitido (RN-12)', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER audit_log_pres_no_update
                    BEFORE UPDATE ON audit_log_presupuestos
                    FOR EACH ROW EXECUTE FUNCTION audit_log_presupuesto_append_only();

                CREATE TRIGGER audit_log_pres_no_delete
                    BEFORE DELETE ON audit_log_presupuestos
                    FOR EACH ROW EXECUTE FUNCTION audit_log_presupuesto_append_only();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS audit_log_pres_no_delete ON audit_log_presupuestos;
                DROP TRIGGER IF EXISTS audit_log_pres_no_update ON audit_log_presupuestos;
                DROP FUNCTION IF EXISTS audit_log_presupuesto_append_only();
                DROP POLICY IF EXISTS tenant_isolation ON audit_log_presupuestos;
                DROP POLICY IF EXISTS tenant_isolation ON ejecuciones_presupuestales;
                DROP POLICY IF EXISTS tenant_isolation ON cuotas_extraordinarias;
                DROP POLICY IF EXISTS tenant_isolation ON pagos_cuotas;
                DROP POLICY IF EXISTS tenant_isolation ON liquidacion_unidades;
                DROP POLICY IF EXISTS tenant_isolation ON liquidaciones;
                DROP POLICY IF EXISTS tenant_isolation ON presupuesto_rubros;
                DROP POLICY IF EXISTS tenant_isolation ON presupuestos;
            ");

            migrationBuilder.DropTable(
                name: "audit_log_presupuestos");

            migrationBuilder.DropTable(
                name: "ejecuciones_presupuestales");

            migrationBuilder.DropTable(
                name: "pagos_cuotas");

            migrationBuilder.DropTable(
                name: "presupuesto_rubros");

            migrationBuilder.DropTable(
                name: "cuotas_extraordinarias");

            migrationBuilder.DropTable(
                name: "liquidacion_unidades");

            migrationBuilder.DropTable(
                name: "liquidaciones");

            migrationBuilder.DropTable(
                name: "presupuestos");
        }
    }
}
