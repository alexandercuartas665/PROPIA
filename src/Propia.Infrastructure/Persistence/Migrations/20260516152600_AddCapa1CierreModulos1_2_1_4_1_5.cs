using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapa1CierreModulos1_2_1_4_1_5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendario_config_usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vista_default = table.Column<int>(type: "integer", nullable: false),
                    ultima_vista = table.Column<int>(type: "integer", nullable: false),
                    filtro_copropiedades_json = table.Column<string>(type: "text", nullable: true),
                    filtro_tipos_json = table.Column<string>(type: "text", nullable: true),
                    ical_token = table.Column<Guid>(type: "uuid", nullable: true),
                    ical_token_generado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    anticipacion_asamblea = table.Column<int>(type: "integer", nullable: false),
                    anticipacion_tarea = table.Column<int>(type: "integer", nullable: false),
                    anticipacion_mantenimiento = table.Column<int>(type: "integer", nullable: false),
                    anticipacion_pqrsd = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendario_config_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_calendario_config_usuarios_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calendario_eventos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    fecha_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    es_dia_completo = table.Column<bool>(type: "boolean", nullable: false),
                    zona_horaria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recordatorio_minutos = table.Column<int>(type: "integer", nullable: true),
                    recordatorio_enviado = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendario_eventos", x => x.id);
                    table.ForeignKey(
                        name: "FK_calendario_eventos_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calendario_eventos_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "org_reportes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    categoria = table.Column<int>(type: "integer", nullable: false),
                    es_plantilla_base = table.Column<bool>(type: "boolean", nullable: false),
                    tiene_datos_nominativos = table.Column<bool>(type: "boolean", nullable: false),
                    configuracion_json = table.Column<string>(type: "text", nullable: false),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_reportes", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_reportes_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transferencias_custodia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    copropiedad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_saliente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organizacion_entrante_id = table.Column<Guid>(type: "uuid", nullable: true),
                    escenario = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    iniciado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_efectiva_saliente = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_vencimiento_ventana = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_corte = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acta_entrega_documento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot_estado_json = table.Column<string>(type: "text", nullable: true),
                    ajuste_facturacion_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferencias_custodia", x => x.id);
                    table.ForeignKey(
                        name: "FK_transferencias_custodia_documentos_acta_entrega_documento_id",
                        column: x => x.acta_entrega_documento_id,
                        principalTable: "documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_transferencias_custodia_tenants_copropiedad_id",
                        column: x => x.copropiedad_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "org_reporte_generaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporte_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen = table.Column<int>(type: "integer", nullable: false),
                    generado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    periodo_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    periodo_hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    resultado_json = table.Column<string>(type: "text", nullable: true),
                    url_pdf = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    url_excel = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    url_expiracion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    error_detalle = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    generado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_reporte_generaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_reporte_generaciones_org_reportes_reporte_id",
                        column: x => x.reporte_id,
                        principalTable: "org_reportes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_org_reporte_generaciones_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transferencia_documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamanio_bytes = table.Column<long>(type: "bigint", nullable: false),
                    url_storage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resultado_validacion_ia = table.Column<int>(type: "integer", nullable: false),
                    detalle_validacion_ia_json = table.Column<string>(type: "text", nullable: true),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferencia_documentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_transferencia_documentos_transferencias_custodia_transferen~",
                        column: x => x.transferencia_id,
                        principalTable: "transferencias_custodia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transferencia_eventos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    actor_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    canal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    detalle_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferencia_eventos", x => x.id);
                    table.ForeignKey(
                        name: "FK_transferencia_eventos_transferencias_custodia_transferencia~",
                        column: x => x.transferencia_id,
                        principalTable: "transferencias_custodia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_calendario_config_usuarios_ical_token",
                table: "calendario_config_usuarios",
                column: "ical_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendario_config_usuarios_organizacion_id",
                table: "calendario_config_usuarios",
                column: "organizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_calendario_config_usuarios_usuario_id_organizacion_id",
                table: "calendario_config_usuarios",
                columns: new[] { "usuario_id", "organizacion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendario_eventos_organizacion_id",
                table: "calendario_eventos",
                column: "organizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_calendario_eventos_organizacion_id_fecha_inicio",
                table: "calendario_eventos",
                columns: new[] { "organizacion_id", "fecha_inicio" });

            migrationBuilder.CreateIndex(
                name: "IX_calendario_eventos_tenant_id",
                table: "calendario_eventos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_reporte_generaciones_organizacion_id_created_at",
                table: "org_reporte_generaciones",
                columns: new[] { "organizacion_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_org_reporte_generaciones_reporte_id",
                table: "org_reporte_generaciones",
                column: "reporte_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_reportes_organizacion_id",
                table: "org_reportes",
                column: "organizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_reportes_organizacion_id_categoria",
                table: "org_reportes",
                columns: new[] { "organizacion_id", "categoria" });

            migrationBuilder.CreateIndex(
                name: "IX_transferencia_documentos_transferencia_id",
                table: "transferencia_documentos",
                column: "transferencia_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencia_eventos_transferencia_id",
                table: "transferencia_eventos",
                column: "transferencia_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencia_eventos_transferencia_id_created_at",
                table: "transferencia_eventos",
                columns: new[] { "transferencia_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_custodia_acta_entrega_documento_id",
                table: "transferencias_custodia",
                column: "acta_entrega_documento_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_custodia_copropiedad_id",
                table: "transferencias_custodia",
                column: "copropiedad_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_custodia_estado",
                table: "transferencias_custodia",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_custodia_organizacion_entrante_id",
                table: "transferencias_custodia",
                column: "organizacion_entrante_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_custodia_organizacion_saliente_id",
                table: "transferencias_custodia",
                column: "organizacion_saliente_id");

            // -----------------------------------------------------------------
            // RN-16: unicidad de proceso activo por copropiedad.
            // Solo aplica a estados no terminales (Iniciado=1, PendienteAprobacion=2,
            // ActaEnValidacion=3, AlertasActivas=4). Ejecutado=5 y Cancelado=6 quedan
            // fuera del indice para permitir multiples ciclos historicos.
            // -----------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX ix_transferencias_custodia_unique_activa
                ON transferencias_custodia (copropiedad_id)
                WHERE estado IN (1, 2, 3, 4);
            ");

            // -----------------------------------------------------------------
            // Historial inmutable: transferencia_eventos no admite UPDATE ni DELETE.
            // Replica el patron usado por pqrsd_historial_estado y documento_auditoria.
            // -----------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION fn_block_update_delete_transferencia_eventos()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'transferencia_eventos es append-only (operacion %)', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_transferencia_eventos_no_update
                BEFORE UPDATE ON transferencia_eventos
                FOR EACH ROW EXECUTE FUNCTION fn_block_update_delete_transferencia_eventos();

                CREATE TRIGGER trg_transferencia_eventos_no_delete
                BEFORE DELETE ON transferencia_eventos
                FOR EACH ROW EXECUTE FUNCTION fn_block_update_delete_transferencia_eventos();
            ");

            // -----------------------------------------------------------------
            // ALTER tabla copropiedad (modulo 1.5 spec seccion 17) -> ya estaba modelado
            // en Tenant.EstadoCustodia desde una migracion previa. Aqui solo dejamos
            // documentado el campo. Si no existiera, deberia agregarse aqui un ALTER.
            // -----------------------------------------------------------------
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_transferencia_eventos_no_update ON transferencia_eventos;
                DROP TRIGGER IF EXISTS trg_transferencia_eventos_no_delete ON transferencia_eventos;
                DROP FUNCTION IF EXISTS fn_block_update_delete_transferencia_eventos();
                DROP INDEX IF EXISTS ix_transferencias_custodia_unique_activa;
            ");

            migrationBuilder.DropTable(
                name: "calendario_config_usuarios");

            migrationBuilder.DropTable(
                name: "calendario_eventos");

            migrationBuilder.DropTable(
                name: "org_reporte_generaciones");

            migrationBuilder.DropTable(
                name: "transferencia_documentos");

            migrationBuilder.DropTable(
                name: "transferencia_eventos");

            migrationBuilder.DropTable(
                name: "org_reportes");

            migrationBuilder.DropTable(
                name: "transferencias_custodia");
        }
    }
}
