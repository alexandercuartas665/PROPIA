using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarteraModulo27 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acuerdos_pago",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    monto_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    capital_incluido = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    intereses_incluidos = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    intereses_condonados = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    numero_cuotas = table.Column<int>(type: "integer", nullable: false),
                    fecha_primera_cuota = table.Column<DateOnly>(type: "date", nullable: false),
                    notas_admin = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    acuerdo_padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aceptacion_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    aceptacion_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    aceptacion_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aceptacion_ip = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    aceptacion_metodo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_expiracion = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acuerdos_pago", x => x.id);
                    table.ForeignKey(
                        name: "FK_acuerdos_pago_acuerdos_pago_acuerdo_padre_id",
                        column: x => x.acuerdo_padre_id,
                        principalTable: "acuerdos_pago",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_acuerdos_pago_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cartera_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    modo_calculo_intereses = table.Column<int>(type: "integer", nullable: false),
                    regla_imputacion = table.Column<int>(type: "integer", nullable: false),
                    paz_salvo_automatico = table.Column<bool>(type: "boolean", nullable: false),
                    paz_salvo_condicionado = table.Column<bool>(type: "boolean", nullable: false),
                    solicitud_residente = table.Column<bool>(type: "boolean", nullable: false),
                    validez_paz_salvo_dias = table.Column<int>(type: "integer", nullable: false),
                    mensaje_paz_salvo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    plazo_aceptacion_dias = table.Column<int>(type: "integer", nullable: false),
                    gracia_cuota_acuerdo = table.Column<int>(type: "integer", nullable: false),
                    tasa_mora_mensual = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    periodo_gracia_dias = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cartera_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cartera_historial",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    datos_adicionales = table.Column<string>(type: "text", nullable: true),
                    realizado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocurrido_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cartera_historial", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "condonaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    monto_condonado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    documento_soporte_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    saldo_antes = table.Column<string>(type: "text", nullable: false),
                    saldo_despues = table.Column<string>(type: "text", nullable: false),
                    autorizado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condonaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_condonaciones_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deuda_detalle",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    liquidacion_unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo = table.Column<DateOnly>(type: "date", nullable: false),
                    concepto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    capital_original = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    capital_pendiente = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    interes_acumulado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_inicio_mora = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deuda_detalle", x => x.id);
                    table.ForeignKey(
                        name: "FK_deuda_detalle_liquidacion_unidades_liquidacion_unidad_id",
                        column: x => x.liquidacion_unidad_id,
                        principalTable: "liquidacion_unidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_deuda_detalle_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "estados_cartera_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    dias_alerta = table.Column<int>(type: "integer", nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    es_inicial = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_estados_cartera_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "paz_salvos_emitidos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    condiciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    fecha_emision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    emitido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emision_tipo = table.Column<int>(type: "integer", nullable: false),
                    documento_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    codigo_verificacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    anulado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_anulacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_anulacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paz_salvos_emitidos", x => x.id);
                    table.ForeignKey(
                        name: "FK_paz_salvos_emitidos_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "acuerdo_cuotas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    acuerdo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_cuota = table.Column<int>(type: "integer", nullable: false),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    capital = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    intereses = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    fecha_pago = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acuerdo_cuotas", x => x.id);
                    table.ForeignKey(
                        name: "FK_acuerdo_cuotas_acuerdos_pago_acuerdo_id",
                        column: x => x.acuerdo_id,
                        principalTable: "acuerdos_pago",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cartera_unidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_gestion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    saldo_capital = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_intereses = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_primer_mora = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_ultimo_pago = table.Column<DateOnly>(type: "date", nullable: true),
                    tiene_acuerdo_vigente = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_cambio_estado_actual = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cartera_unidades", x => x.id);
                    table.ForeignKey(
                        name: "FK_cartera_unidades_estados_cartera_config_estado_gestion_id",
                        column: x => x.estado_gestion_id,
                        principalTable: "estados_cartera_config",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cartera_unidades_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_acuerdo_cuotas_acuerdo_id",
                table: "acuerdo_cuotas",
                column: "acuerdo_id");

            migrationBuilder.CreateIndex(
                name: "IX_acuerdo_cuotas_tenant_id",
                table: "acuerdo_cuotas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_acuerdos_pago_acuerdo_padre_id",
                table: "acuerdos_pago",
                column: "acuerdo_padre_id");

            migrationBuilder.CreateIndex(
                name: "IX_acuerdos_pago_tenant_id",
                table: "acuerdos_pago",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_acuerdos_pago_tenant_id_estado",
                table: "acuerdos_pago",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_acuerdos_pago_tenant_id_unidad_privada_id",
                table: "acuerdos_pago",
                columns: new[] { "tenant_id", "unidad_privada_id" });

            migrationBuilder.CreateIndex(
                name: "IX_acuerdos_pago_unidad_privada_id",
                table: "acuerdos_pago",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_cartera_config_tenant_id",
                table: "cartera_config",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cartera_historial_tenant_id",
                table: "cartera_historial",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_cartera_historial_tenant_id_unidad_privada_id_ocurrido_at",
                table: "cartera_historial",
                columns: new[] { "tenant_id", "unidad_privada_id", "ocurrido_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cartera_unidades_estado_gestion_id",
                table: "cartera_unidades",
                column: "estado_gestion_id");

            migrationBuilder.CreateIndex(
                name: "IX_cartera_unidades_tenant_id",
                table: "cartera_unidades",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_cartera_unidades_tenant_id_unidad_privada_id",
                table: "cartera_unidades",
                columns: new[] { "tenant_id", "unidad_privada_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cartera_unidades_unidad_privada_id",
                table: "cartera_unidades",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_condonaciones_tenant_id",
                table: "condonaciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_condonaciones_tenant_id_unidad_privada_id",
                table: "condonaciones",
                columns: new[] { "tenant_id", "unidad_privada_id" });

            migrationBuilder.CreateIndex(
                name: "IX_condonaciones_unidad_privada_id",
                table: "condonaciones",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_deuda_detalle_liquidacion_unidad_id",
                table: "deuda_detalle",
                column: "liquidacion_unidad_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_deuda_detalle_tenant_id",
                table: "deuda_detalle",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_deuda_detalle_tenant_id_unidad_privada_id",
                table: "deuda_detalle",
                columns: new[] { "tenant_id", "unidad_privada_id" });

            migrationBuilder.CreateIndex(
                name: "IX_deuda_detalle_unidad_privada_id",
                table: "deuda_detalle",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_estados_cartera_config_tenant_id",
                table: "estados_cartera_config",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_estados_cartera_config_tenant_id_nombre",
                table: "estados_cartera_config",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paz_salvos_emitidos_codigo_verificacion",
                table: "paz_salvos_emitidos",
                column: "codigo_verificacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paz_salvos_emitidos_tenant_id",
                table: "paz_salvos_emitidos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_paz_salvos_emitidos_tenant_id_unidad_privada_id",
                table: "paz_salvos_emitidos",
                columns: new[] { "tenant_id", "unidad_privada_id" });

            migrationBuilder.CreateIndex(
                name: "IX_paz_salvos_emitidos_unidad_privada_id",
                table: "paz_salvos_emitidos",
                column: "unidad_privada_id");

            // RLS + GRANTs para las 9 tablas del modulo 2.7
            migrationBuilder.Sql(@"
                ALTER TABLE estados_cartera_config ENABLE ROW LEVEL SECURITY;
                ALTER TABLE estados_cartera_config FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON estados_cartera_config
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON estados_cartera_config TO propia_app;

                ALTER TABLE cartera_config ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cartera_config FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON cartera_config
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON cartera_config TO propia_app;

                ALTER TABLE cartera_unidades ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cartera_unidades FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON cartera_unidades
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON cartera_unidades TO propia_app;

                ALTER TABLE deuda_detalle ENABLE ROW LEVEL SECURITY;
                ALTER TABLE deuda_detalle FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON deuda_detalle
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON deuda_detalle TO propia_app;

                ALTER TABLE acuerdos_pago ENABLE ROW LEVEL SECURITY;
                ALTER TABLE acuerdos_pago FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON acuerdos_pago
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON acuerdos_pago TO propia_app;

                ALTER TABLE acuerdo_cuotas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE acuerdo_cuotas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON acuerdo_cuotas
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON acuerdo_cuotas TO propia_app;

                -- paz_salvos_emitidos: solo INSERT y SELECT (RN-08 - inmutables, solo se anulan via UPDATE de campos especificos)
                ALTER TABLE paz_salvos_emitidos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE paz_salvos_emitidos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON paz_salvos_emitidos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE ON paz_salvos_emitidos TO propia_app;

                -- condonaciones: append-only (RN-10)
                ALTER TABLE condonaciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE condonaciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON condonaciones
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT ON condonaciones TO propia_app;

                -- cartera_historial: append-only
                ALTER TABLE cartera_historial ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cartera_historial FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON cartera_historial
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT ON cartera_historial TO propia_app;
            ");

            // Triggers append-only en condonaciones y cartera_historial (RN-10 + trazabilidad)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION cartera_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'Tabla append-only: % no permitido en %', TG_OP, TG_TABLE_NAME;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER condonaciones_no_update
                    BEFORE UPDATE ON condonaciones
                    FOR EACH ROW EXECUTE FUNCTION cartera_append_only();
                CREATE TRIGGER condonaciones_no_delete
                    BEFORE DELETE ON condonaciones
                    FOR EACH ROW EXECUTE FUNCTION cartera_append_only();

                CREATE TRIGGER cartera_historial_no_update
                    BEFORE UPDATE ON cartera_historial
                    FOR EACH ROW EXECUTE FUNCTION cartera_append_only();
                CREATE TRIGGER cartera_historial_no_delete
                    BEFORE DELETE ON cartera_historial
                    FOR EACH ROW EXECUTE FUNCTION cartera_append_only();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS cartera_historial_no_delete ON cartera_historial;
                DROP TRIGGER IF EXISTS cartera_historial_no_update ON cartera_historial;
                DROP TRIGGER IF EXISTS condonaciones_no_delete ON condonaciones;
                DROP TRIGGER IF EXISTS condonaciones_no_update ON condonaciones;
                DROP FUNCTION IF EXISTS cartera_append_only();
            ");

            migrationBuilder.DropTable(
                name: "acuerdo_cuotas");

            migrationBuilder.DropTable(
                name: "cartera_config");

            migrationBuilder.DropTable(
                name: "cartera_historial");

            migrationBuilder.DropTable(
                name: "cartera_unidades");

            migrationBuilder.DropTable(
                name: "condonaciones");

            migrationBuilder.DropTable(
                name: "deuda_detalle");

            migrationBuilder.DropTable(
                name: "paz_salvos_emitidos");

            migrationBuilder.DropTable(
                name: "acuerdos_pago");

            migrationBuilder.DropTable(
                name: "estados_cartera_config");
        }
    }
}
