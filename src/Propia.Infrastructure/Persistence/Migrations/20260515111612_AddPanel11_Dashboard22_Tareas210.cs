using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPanel11_Dashboard22_Tareas210 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "actividad_feed",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    actor_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    modulo_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    url_item = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ocurrido_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actividad_feed", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alertas_copropiedad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    severidad = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    url_accion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    modulo_origen_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    resuelta_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertas_copropiedad", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "panel_configuracion_usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vista_default = table.Column<int>(type: "integer", nullable: false),
                    kpis_globales = table.Column<string>(type: "text", nullable: false),
                    tarjeta_indicadores = table.Column<string>(type: "text", nullable: false),
                    feed_activo = table.Column<bool>(type: "boolean", nullable: false),
                    proximos_eventos_activo = table.Column<bool>(type: "boolean", nullable: false),
                    proximos_eventos_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_panel_configuracion_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_panel_configuracion_usuarios_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "panel_feed_eventos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    entidad_tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    url_accion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ocurrido_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_panel_feed_eventos", x => x.id);
                    table.ForeignKey(
                        name: "FK_panel_feed_eventos_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_panel_feed_eventos_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "panel_snapshot_copropiedades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_salud = table.Column<int>(type: "integer", nullable: false),
                    alertas_criticas = table.Column<int>(type: "integer", nullable: false),
                    tareas_vencidas = table.Column<int>(type: "integer", nullable: false),
                    pqrsd_sin_responder = table.Column<int>(type: "integer", nullable: false),
                    recaudo_mes_porcentaje = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    cartera_vencida_cop = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    proximo_evento_fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    proximo_evento_tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    proximo_evento_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    calculado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_panel_snapshot_copropiedades", x => x.id);
                    table.ForeignKey(
                        name: "FK_panel_snapshot_copropiedades_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_panel_snapshot_copropiedades_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tarea_estados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    es_terminal = table.Column<bool>(type: "boolean", nullable: false),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarea_estados", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tarea_etiquetas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarea_etiquetas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tareas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_tarea = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    estado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prioridad = table.Column<int>(type: "integer", nullable: false),
                    asignado_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_completada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origen = table.Column<int>(type: "integer", nullable: false),
                    modulo_origen_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    modulo_origen_entidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_cancelacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tareas", x => x.id);
                    table.ForeignKey(
                        name: "FK_tareas_personas_asignado_persona_id",
                        column: x => x.asignado_persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tareas_tarea_estados_estado_id",
                        column: x => x.estado_id,
                        principalTable: "tarea_estados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tareas_tareas_padre_id",
                        column: x => x.padre_id,
                        principalTable: "tareas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tarea_colaboradores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarea_colaboradores", x => x.id);
                    table.ForeignKey(
                        name: "FK_tarea_colaboradores_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tarea_colaboradores_tareas_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tarea_comentarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarea_comentarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_tarea_comentarios_tareas_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tarea_etiqueta_asignaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etiqueta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarea_etiqueta_asignaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_tarea_etiqueta_asignaciones_tarea_etiquetas_etiqueta_id",
                        column: x => x.etiqueta_id,
                        principalTable: "tarea_etiquetas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tarea_etiqueta_asignaciones_tareas_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tarea_historial",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    valor_anterior = table.Column<string>(type: "text", nullable: true),
                    valor_nuevo = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_tarea_historial", x => x.id);
                    table.ForeignKey(
                        name: "FK_tarea_historial_tareas_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_actividad_feed_tenant_id",
                table: "actividad_feed",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_actividad_feed_tenant_id_ocurrido_at",
                table: "actividad_feed",
                columns: new[] { "tenant_id", "ocurrido_at" });

            migrationBuilder.CreateIndex(
                name: "IX_alertas_copropiedad_tenant_id",
                table: "alertas_copropiedad",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_alertas_copropiedad_tenant_id_activa",
                table: "alertas_copropiedad",
                columns: new[] { "tenant_id", "activa" });

            migrationBuilder.CreateIndex(
                name: "IX_panel_configuracion_usuarios_organizacion_id",
                table: "panel_configuracion_usuarios",
                column: "organizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_panel_configuracion_usuarios_usuario_id_organizacion_id",
                table: "panel_configuracion_usuarios",
                columns: new[] { "usuario_id", "organizacion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_panel_feed_eventos_organizacion_id",
                table: "panel_feed_eventos",
                column: "organizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_panel_feed_eventos_organizacion_id_ocurrido_at",
                table: "panel_feed_eventos",
                columns: new[] { "organizacion_id", "ocurrido_at" });

            migrationBuilder.CreateIndex(
                name: "IX_panel_feed_eventos_tenant_id",
                table: "panel_feed_eventos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_panel_snapshot_copropiedades_organizacion_id",
                table: "panel_snapshot_copropiedades",
                column: "organizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_panel_snapshot_copropiedades_organizacion_id_estado_salud",
                table: "panel_snapshot_copropiedades",
                columns: new[] { "organizacion_id", "estado_salud" });

            migrationBuilder.CreateIndex(
                name: "IX_panel_snapshot_copropiedades_organizacion_id_tenant_id",
                table: "panel_snapshot_copropiedades",
                columns: new[] { "organizacion_id", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_panel_snapshot_copropiedades_tenant_id",
                table: "panel_snapshot_copropiedades",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_colaboradores_persona_id",
                table: "tarea_colaboradores",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_colaboradores_tarea_id_persona_id",
                table: "tarea_colaboradores",
                columns: new[] { "tarea_id", "persona_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tarea_colaboradores_tenant_id",
                table: "tarea_colaboradores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_comentarios_tarea_id",
                table: "tarea_comentarios",
                column: "tarea_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_comentarios_tenant_id",
                table: "tarea_comentarios",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_estados_tenant_id",
                table: "tarea_estados",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_estados_tenant_id_nombre",
                table: "tarea_estados",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tarea_etiqueta_asignaciones_etiqueta_id",
                table: "tarea_etiqueta_asignaciones",
                column: "etiqueta_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_etiqueta_asignaciones_tarea_id_etiqueta_id",
                table: "tarea_etiqueta_asignaciones",
                columns: new[] { "tarea_id", "etiqueta_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tarea_etiqueta_asignaciones_tenant_id",
                table: "tarea_etiqueta_asignaciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_etiquetas_tenant_id",
                table: "tarea_etiquetas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_etiquetas_tenant_id_nombre",
                table: "tarea_etiquetas",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tarea_historial_tarea_id_ocurrido_at",
                table: "tarea_historial",
                columns: new[] { "tarea_id", "ocurrido_at" });

            migrationBuilder.CreateIndex(
                name: "IX_tarea_historial_tenant_id",
                table: "tarea_historial",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_asignado_persona_id",
                table: "tareas",
                column: "asignado_persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_estado_id",
                table: "tareas",
                column: "estado_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_padre_id",
                table: "tareas",
                column: "padre_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_tenant_id",
                table: "tareas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_tenant_id_asignado_persona_id",
                table: "tareas",
                columns: new[] { "tenant_id", "asignado_persona_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tareas_tenant_id_estado_id",
                table: "tareas",
                columns: new[] { "tenant_id", "estado_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tareas_tenant_id_numero_tarea",
                table: "tareas",
                columns: new[] { "tenant_id", "numero_tarea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tareas_tenant_id_padre_id",
                table: "tareas",
                columns: new[] { "tenant_id", "padre_id" });

            // GRANTs + RLS para tablas de TENANT (2.2 y 2.10)
            migrationBuilder.Sql(@"
                ALTER TABLE alertas_copropiedad ENABLE ROW LEVEL SECURITY;
                ALTER TABLE alertas_copropiedad FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON alertas_copropiedad
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON alertas_copropiedad TO propia_app;

                ALTER TABLE actividad_feed ENABLE ROW LEVEL SECURITY;
                ALTER TABLE actividad_feed FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON actividad_feed
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON actividad_feed TO propia_app;

                ALTER TABLE tarea_estados ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tarea_estados FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tarea_estados
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tarea_estados TO propia_app;

                ALTER TABLE tarea_etiquetas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tarea_etiquetas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tarea_etiquetas
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tarea_etiquetas TO propia_app;

                ALTER TABLE tarea_etiqueta_asignaciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tarea_etiqueta_asignaciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tarea_etiqueta_asignaciones
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tarea_etiqueta_asignaciones TO propia_app;

                ALTER TABLE tareas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tareas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tareas
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tareas TO propia_app;

                ALTER TABLE tarea_colaboradores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tarea_colaboradores FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tarea_colaboradores
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tarea_colaboradores TO propia_app;

                ALTER TABLE tarea_comentarios ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tarea_comentarios FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tarea_comentarios
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tarea_comentarios TO propia_app;

                ALTER TABLE tarea_historial ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tarea_historial FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tarea_historial
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT ON tarea_historial TO propia_app;
            ");

            // GRANTs para tablas GLOBALES del modulo 1.1
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON panel_snapshot_copropiedades TO propia_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON panel_configuracion_usuarios TO propia_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON panel_feed_eventos TO propia_app;
            ");

            // Trigger append-only en tarea_historial
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION tarea_historial_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'tarea_historial es append-only: % no permitido', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER tarea_historial_no_update
                    BEFORE UPDATE ON tarea_historial
                    FOR EACH ROW EXECUTE FUNCTION tarea_historial_append_only();

                CREATE TRIGGER tarea_historial_no_delete
                    BEFORE DELETE ON tarea_historial
                    FOR EACH ROW EXECUTE FUNCTION tarea_historial_append_only();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS tarea_historial_no_delete ON tarea_historial;
                DROP TRIGGER IF EXISTS tarea_historial_no_update ON tarea_historial;
                DROP FUNCTION IF EXISTS tarea_historial_append_only();
            ");

            migrationBuilder.DropTable(
                name: "actividad_feed");

            migrationBuilder.DropTable(
                name: "alertas_copropiedad");

            migrationBuilder.DropTable(
                name: "panel_configuracion_usuarios");

            migrationBuilder.DropTable(
                name: "panel_feed_eventos");

            migrationBuilder.DropTable(
                name: "panel_snapshot_copropiedades");

            migrationBuilder.DropTable(
                name: "tarea_colaboradores");

            migrationBuilder.DropTable(
                name: "tarea_comentarios");

            migrationBuilder.DropTable(
                name: "tarea_etiqueta_asignaciones");

            migrationBuilder.DropTable(
                name: "tarea_historial");

            migrationBuilder.DropTable(
                name: "tarea_etiquetas");

            migrationBuilder.DropTable(
                name: "tareas");

            migrationBuilder.DropTable(
                name: "tarea_estados");
        }
    }
}
