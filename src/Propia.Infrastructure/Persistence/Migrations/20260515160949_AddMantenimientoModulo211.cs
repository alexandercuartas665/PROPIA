using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMantenimientoModulo211 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Estado del activo - fuente unica de verdad en 2.3 (escrito por 2.11).
            // Default 1 = Activa (zonas) / Operativo (equipos) para filas existentes.
            migrationBuilder.AddColumn<int>(
                name: "estado",
                table: "zonas_comunes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "estado",
                table: "equipos_activos",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "mantenimiento_planes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_tipo = table.Column<int>(type: "integer", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    frecuencia = table.Column<int>(type: "integer", nullable: false),
                    frecuencia_dias = table.Column<int>(type: "integer", nullable: true),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    proxima_ejecucion = table.Column<DateOnly>(type: "date", nullable: false),
                    proveedor_preferido_id = table.Column<Guid>(type: "uuid", nullable: true),
                    disparo = table.Column<int>(type: "integer", nullable: false),
                    dias_alerta_previo = table.Column<int>(type: "integer", nullable: false),
                    genera_notif_residentes = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mantenimiento_planes", x => x.id);
                    table.ForeignKey(
                        name: "FK_mantenimiento_planes_personas_proveedor_preferido_id",
                        column: x => x.proveedor_preferido_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "mantenimiento_intervenciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    activo_tipo = table.Column<int>(type: "integer", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origen = table.Column<int>(type: "integer", nullable: false),
                    origen_referencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    prioridad = table.Column<int>(type: "integer", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsable_interno_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_programada = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_inicio_real = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_cierre = table.Column<DateOnly>(type: "date", nullable: true),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cambio_estado_activo = table.Column<bool>(type: "boolean", nullable: false),
                    estado_activo_nuevo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notificar_residentes = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_cancelacion = table.Column<string>(type: "text", nullable: true),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mantenimiento_intervenciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_mantenimiento_intervenciones_mantenimiento_planes_plan_id",
                        column: x => x.plan_id,
                        principalTable: "mantenimiento_planes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_mantenimiento_intervenciones_personas_proveedor_id",
                        column: x => x.proveedor_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_mantenimiento_intervenciones_personas_responsable_interno_id",
                        column: x => x.responsable_interno_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_mantenimiento_intervenciones_tareas_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "mantenimiento_bitacora",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    intervencion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_autor = table.Column<int>(type: "integer", nullable: false),
                    contenido = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mantenimiento_bitacora", x => x.id);
                    table.ForeignKey(
                        name: "FK_mantenimiento_bitacora_mantenimiento_intervenciones_interve~",
                        column: x => x.intervencion_id,
                        principalTable: "mantenimiento_intervenciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mantenimiento_historial_estado",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo_tipo = table.Column<int>(type: "integer", nullable: false),
                    activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intervencion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado_anterior = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    estado_nuevo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: true),
                    notificado_residentes = table.Column<bool>(type: "boolean", nullable: false),
                    actor_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mantenimiento_historial_estado", x => x.id);
                    table.ForeignKey(
                        name: "FK_mantenimiento_historial_estado_mantenimiento_intervenciones~",
                        column: x => x.intervencion_id,
                        principalTable: "mantenimiento_intervenciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "mantenimiento_adjuntos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bitacora_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    url_storage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mantenimiento_adjuntos", x => x.id);
                    table.ForeignKey(
                        name: "FK_mantenimiento_adjuntos_mantenimiento_bitacora_bitacora_id",
                        column: x => x.bitacora_id,
                        principalTable: "mantenimiento_bitacora",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_adjuntos_bitacora_id",
                table: "mantenimiento_adjuntos",
                column: "bitacora_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_adjuntos_tenant_id",
                table: "mantenimiento_adjuntos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_bitacora_intervencion_id",
                table: "mantenimiento_bitacora",
                column: "intervencion_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_bitacora_tenant_id",
                table: "mantenimiento_bitacora",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_bitacora_tenant_id_intervencion_id_created_at",
                table: "mantenimiento_bitacora",
                columns: new[] { "tenant_id", "intervencion_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_historial_estado_intervencion_id",
                table: "mantenimiento_historial_estado",
                column: "intervencion_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_historial_estado_tenant_id",
                table: "mantenimiento_historial_estado",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_historial_estado_tenant_id_activo_tipo_activo~",
                table: "mantenimiento_historial_estado",
                columns: new[] { "tenant_id", "activo_tipo", "activo_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_intervenciones_plan_id",
                table: "mantenimiento_intervenciones",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_intervenciones_proveedor_id",
                table: "mantenimiento_intervenciones",
                column: "proveedor_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_intervenciones_responsable_interno_id",
                table: "mantenimiento_intervenciones",
                column: "responsable_interno_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_intervenciones_tarea_id",
                table: "mantenimiento_intervenciones",
                column: "tarea_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_intervenciones_tenant_id",
                table: "mantenimiento_intervenciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_intervenciones_tenant_id_activo_tipo_activo_id",
                table: "mantenimiento_intervenciones",
                columns: new[] { "tenant_id", "activo_tipo", "activo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_intervenciones_tenant_id_codigo",
                table: "mantenimiento_intervenciones",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_intervenciones_tenant_id_estado",
                table: "mantenimiento_intervenciones",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_intervenciones_tenant_id_fecha_programada",
                table: "mantenimiento_intervenciones",
                columns: new[] { "tenant_id", "fecha_programada" });

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_planes_proveedor_preferido_id",
                table: "mantenimiento_planes",
                column: "proveedor_preferido_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_planes_tenant_id",
                table: "mantenimiento_planes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_planes_tenant_id_activo_tipo_activo_id",
                table: "mantenimiento_planes",
                columns: new[] { "tenant_id", "activo_tipo", "activo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_mantenimiento_planes_tenant_id_proxima_ejecucion",
                table: "mantenimiento_planes",
                columns: new[] { "tenant_id", "proxima_ejecucion" });

            // -----------------------------------------------------------------
            // RLS + GRANTs + triggers append-only (Spec 2.11 - notas para el dev)
            // -----------------------------------------------------------------

            // RLS: habilitar y forzar en todas las tablas multi-tenant del modulo.
            foreach (var tabla in new[] {
                "mantenimiento_planes",
                "mantenimiento_intervenciones",
                "mantenimiento_bitacora",
                "mantenimiento_adjuntos",
                "mantenimiento_historial_estado"
            })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {tabla}
                        USING (tenant_id::text = current_setting('app.tenant_id', true))
                        WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {tabla} TO propia_app;
                ");
            }

            // Trigger append-only sobre mantenimiento_bitacora (RN-06).
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION mantenimiento_bitacora_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'mantenimiento_bitacora es append-only (RN-06 spec 2.11). No se permite % sobre id %.', TG_OP, OLD.id;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER mantenimiento_bitacora_no_update
                    BEFORE UPDATE ON mantenimiento_bitacora
                    FOR EACH ROW EXECUTE FUNCTION mantenimiento_bitacora_append_only();

                CREATE TRIGGER mantenimiento_bitacora_no_delete
                    BEFORE DELETE ON mantenimiento_bitacora
                    FOR EACH ROW EXECUTE FUNCTION mantenimiento_bitacora_append_only();
            ");

            // Trigger append-only sobre mantenimiento_historial_estado (RN-14).
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION mantenimiento_historial_estado_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'mantenimiento_historial_estado es append-only (RN-14 spec 2.11). No se permite % sobre id %.', TG_OP, OLD.id;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER mantenimiento_historial_estado_no_update
                    BEFORE UPDATE ON mantenimiento_historial_estado
                    FOR EACH ROW EXECUTE FUNCTION mantenimiento_historial_estado_append_only();

                CREATE TRIGGER mantenimiento_historial_estado_no_delete
                    BEFORE DELETE ON mantenimiento_historial_estado
                    FOR EACH ROW EXECUTE FUNCTION mantenimiento_historial_estado_append_only();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Limpieza de triggers append-only y funciones asociadas.
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS mantenimiento_historial_estado_no_delete ON mantenimiento_historial_estado;
                DROP TRIGGER IF EXISTS mantenimiento_historial_estado_no_update ON mantenimiento_historial_estado;
                DROP FUNCTION IF EXISTS mantenimiento_historial_estado_append_only();
                DROP TRIGGER IF EXISTS mantenimiento_bitacora_no_delete ON mantenimiento_bitacora;
                DROP TRIGGER IF EXISTS mantenimiento_bitacora_no_update ON mantenimiento_bitacora;
                DROP FUNCTION IF EXISTS mantenimiento_bitacora_append_only();
            ");

            migrationBuilder.DropTable(
                name: "mantenimiento_adjuntos");

            migrationBuilder.DropTable(
                name: "mantenimiento_historial_estado");

            migrationBuilder.DropTable(
                name: "mantenimiento_bitacora");

            migrationBuilder.DropTable(
                name: "mantenimiento_intervenciones");

            migrationBuilder.DropTable(
                name: "mantenimiento_planes");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "zonas_comunes");

            migrationBuilder.DropColumn(
                name: "estado",
                table: "equipos_activos");
        }
    }
}
