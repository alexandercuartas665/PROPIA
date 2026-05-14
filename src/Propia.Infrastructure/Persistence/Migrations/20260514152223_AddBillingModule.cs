using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dias_gracia = table.Column<int>(type: "integer", nullable: false),
                    dia_alerta_mora1 = table.Column<int>(type: "integer", nullable: false),
                    dia_alerta_mora2 = table.Column<int>(type: "integer", nullable: false),
                    dia_suspension = table.Column<int>(type: "integer", nullable: false),
                    dia_alerta_cancelacion = table.Column<int>(type: "integer", nullable: false),
                    dia_cancelacion = table.Column<int>(type: "integer", nullable: false),
                    reintentos_cobro = table.Column<int>(type: "integer", nullable: false),
                    dias_entre_reintentos = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[1,3,7]'::jsonb"),
                    dias_preaviso_cobro = table.Column<int>(type: "integer", nullable: false),
                    retencion_datos_meses = table.Column<int>(type: "integer", nullable: false),
                    retencion_facturas_anios = table.Column<int>(type: "integer", nullable: false),
                    impuesto_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    proveedor_contable = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cupones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    aplicacion = table.Column<int>(type: "integer", nullable: false),
                    meses_aplicacion = table.Column<int>(type: "integer", nullable: true),
                    vigencia_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    usos_maximos = table.Column<int>(type: "integer", nullable: true),
                    usos_actuales = table.Column<int>(type: "integer", nullable: false),
                    planes_aplicables = table.Column<string>(type: "jsonb", nullable: true),
                    acumulable = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cupones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "metodos_pago",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    copropiedad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    token_wompi = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ultimos_digitos = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    marca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    banco = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    es_predeterminado = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metodos_pago", x => x.id);
                    table.CheckConstraint("ck_metodo_pago_owner", "(organizacion_id IS NOT NULL AND copropiedad_id IS NULL) OR (organizacion_id IS NULL AND copropiedad_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_metodos_pago_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_metodos_pago_tenants_copropiedad_id",
                        column: x => x.copropiedad_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "planes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    fee_base = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    fee_variable_por_unidad = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ciclo_mensual = table.Column<bool>(type: "boolean", nullable: false),
                    ciclo_anual = table.Column<bool>(type: "boolean", nullable: false),
                    descuento_anual_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    limite_unidades = table.Column<int>(type: "integer", nullable: true),
                    limite_usuarios = table.Column<int>(type: "integer", nullable: true),
                    limite_storage_gb = table.Column<int>(type: "integer", nullable: true),
                    dias_trial = table.Column<int>(type: "integer", nullable: false),
                    modulos_incluidos = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "suscripciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    copropiedad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_aniversario = table.Column<int>(type: "integer", nullable: false),
                    fecha_proximo_cobro = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin_trial = table.Column<DateOnly>(type: "date", nullable: true),
                    metodo_pago_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cupon_id = table.Column<Guid>(type: "uuid", nullable: true),
                    credito_a_favor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suscripciones", x => x.id);
                    table.CheckConstraint("ck_suscripcion_owner", "(organizacion_id IS NOT NULL AND copropiedad_id IS NULL) OR (organizacion_id IS NULL AND copropiedad_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_suscripciones_cupones_cupon_id",
                        column: x => x.cupon_id,
                        principalTable: "cupones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_suscripciones_metodos_pago_metodo_pago_id",
                        column: x => x.metodo_pago_id,
                        principalTable: "metodos_pago",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_suscripciones_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_suscripciones_planes_plan_id",
                        column: x => x.plan_id,
                        principalTable: "planes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_suscripciones_tenants_copropiedad_id",
                        column: x => x.copropiedad_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "facturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    suscripcion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_factura = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cufe = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    referencia_externa = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    periodo_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    periodo_hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    descuento = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    impuesto_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    impuesto_valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    fecha_emision = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_pago = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    metodo_pago_id = table.Column<Guid>(type: "uuid", nullable: true),
                    wompi_transaction_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facturas", x => x.id);
                    table.ForeignKey(
                        name: "FK_facturas_metodos_pago_metodo_pago_id",
                        column: x => x.metodo_pago_id,
                        principalTable: "metodos_pago",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_facturas_suscripciones_suscripcion_id",
                        column: x => x.suscripcion_id,
                        principalTable: "suscripciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "suscripcion_historial",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    suscripcion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    origen = table.Column<int>(type: "integer", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plan_anterior_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plan_nuevo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado_anterior = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    estado_nuevo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    monto_prorrateo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    credito_generado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    notas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suscripcion_historial", x => x.id);
                    table.ForeignKey(
                        name: "FK_suscripcion_historial_suscripciones_suscripcion_id",
                        column: x => x.suscripcion_id,
                        principalTable: "suscripciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intentos_cobro",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    factura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suscripcion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_intento = table.Column<int>(type: "integer", nullable: false),
                    fecha_intento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resultado = table.Column<int>(type: "integer", nullable: false),
                    codigo_error = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    descripcion_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    wompi_response = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intentos_cobro", x => x.id);
                    table.ForeignKey(
                        name: "FK_intentos_cobro_facturas_factura_id",
                        column: x => x.factura_id,
                        principalTable: "facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_intentos_cobro_suscripciones_suscripcion_id",
                        column: x => x.suscripcion_id,
                        principalTable: "suscripciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cupones_codigo",
                table: "cupones",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_facturas_estado",
                table: "facturas",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "IX_facturas_fecha_emision",
                table: "facturas",
                column: "fecha_emision");

            migrationBuilder.CreateIndex(
                name: "IX_facturas_metodo_pago_id",
                table: "facturas",
                column: "metodo_pago_id");

            migrationBuilder.CreateIndex(
                name: "IX_facturas_numero_factura",
                table: "facturas",
                column: "numero_factura",
                unique: true,
                filter: "numero_factura IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_facturas_suscripcion_id",
                table: "facturas",
                column: "suscripcion_id");

            migrationBuilder.CreateIndex(
                name: "IX_intentos_cobro_factura_id_numero_intento",
                table: "intentos_cobro",
                columns: new[] { "factura_id", "numero_intento" });

            migrationBuilder.CreateIndex(
                name: "IX_intentos_cobro_resultado",
                table: "intentos_cobro",
                column: "resultado");

            migrationBuilder.CreateIndex(
                name: "IX_intentos_cobro_suscripcion_id",
                table: "intentos_cobro",
                column: "suscripcion_id");

            migrationBuilder.CreateIndex(
                name: "IX_metodos_pago_copropiedad_id",
                table: "metodos_pago",
                column: "copropiedad_id");

            migrationBuilder.CreateIndex(
                name: "IX_metodos_pago_organizacion_id",
                table: "metodos_pago",
                column: "organizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_planes_estado",
                table: "planes",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "IX_suscripcion_historial_created_at",
                table: "suscripcion_historial",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_suscripcion_historial_suscripcion_id",
                table: "suscripcion_historial",
                column: "suscripcion_id");

            migrationBuilder.CreateIndex(
                name: "IX_suscripciones_copropiedad_id",
                table: "suscripciones",
                column: "copropiedad_id");

            migrationBuilder.CreateIndex(
                name: "IX_suscripciones_cupon_id",
                table: "suscripciones",
                column: "cupon_id");

            migrationBuilder.CreateIndex(
                name: "IX_suscripciones_estado",
                table: "suscripciones",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "IX_suscripciones_fecha_proximo_cobro",
                table: "suscripciones",
                column: "fecha_proximo_cobro");

            migrationBuilder.CreateIndex(
                name: "IX_suscripciones_metodo_pago_id",
                table: "suscripciones",
                column: "metodo_pago_id");

            migrationBuilder.CreateIndex(
                name: "IX_suscripciones_organizacion_id",
                table: "suscripciones",
                column: "organizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_suscripciones_plan_id",
                table: "suscripciones",
                column: "plan_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_config");

            migrationBuilder.DropTable(
                name: "intentos_cobro");

            migrationBuilder.DropTable(
                name: "suscripcion_historial");

            migrationBuilder.DropTable(
                name: "facturas");

            migrationBuilder.DropTable(
                name: "suscripciones");

            migrationBuilder.DropTable(
                name: "cupones");

            migrationBuilder.DropTable(
                name: "metodos_pago");

            migrationBuilder.DropTable(
                name: "planes");
        }
    }
}
