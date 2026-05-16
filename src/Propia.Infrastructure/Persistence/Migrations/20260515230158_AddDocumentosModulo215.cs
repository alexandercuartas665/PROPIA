using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentosModulo215 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documento_categorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    icono = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_categorias", x => x.id);
                    table.ForeignKey(
                        name: "FK_documento_categorias_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "documento_etiquetas_catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_etiquetas_catalogo", x => x.id);
                    table.ForeignKey(
                        name: "FK_documento_etiquetas_catalogo_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "documento_carpetas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_carpetas", x => x.id);
                    table.ForeignKey(
                        name: "FK_documento_carpetas_documento_carpetas_padre_id",
                        column: x => x.padre_id,
                        principalTable: "documento_carpetas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documento_carpetas_documento_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "documento_categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documento_auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    detalle_json = table.Column<string>(type: "text", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocurrido_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documento_consumo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    dispositivo = table.Column<int>(type: "integer", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocurrido_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_consumo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documento_destacados_personal",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_destacados_personal", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documento_etiqueta_asignaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etiqueta_catalogo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_etiqueta_asignaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_documento_etiqueta_asignaciones_documento_etiquetas_catalog~",
                        column: x => x.etiqueta_catalogo_id,
                        principalTable: "documento_etiquetas_catalogo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documento_versiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
                    url_storage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    notas_cambio = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_versiones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carpeta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    nombre_archivo_original = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    origen = table.Column<int>(type: "integer", nullable: false),
                    origen_entidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    visibilidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version_actual_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_versiones = table.Column<int>(type: "integer", nullable: false),
                    destacado = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_documentos_documento_carpetas_carpeta_id",
                        column: x => x.carpeta_id,
                        principalTable: "documento_carpetas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documentos_documento_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "documento_categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documentos_documento_versiones_version_actual_id",
                        column: x => x.version_actual_id,
                        principalTable: "documento_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documento_auditoria_documento_id_ocurrido_at",
                table: "documento_auditoria",
                columns: new[] { "documento_id", "ocurrido_at" });

            migrationBuilder.CreateIndex(
                name: "IX_documento_auditoria_tenant_id",
                table: "documento_auditoria",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_carpetas_categoria_id",
                table: "documento_carpetas",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_carpetas_padre_id",
                table: "documento_carpetas",
                column: "padre_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_carpetas_tenant_id",
                table: "documento_carpetas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_carpetas_tenant_id_categoria_id",
                table: "documento_carpetas",
                columns: new[] { "tenant_id", "categoria_id" });

            migrationBuilder.CreateIndex(
                name: "IX_documento_carpetas_tenant_id_padre_id",
                table: "documento_carpetas",
                columns: new[] { "tenant_id", "padre_id" });

            migrationBuilder.CreateIndex(
                name: "IX_documento_categorias_tenant_id",
                table: "documento_categorias",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_categorias_tenant_id_nombre",
                table: "documento_categorias",
                columns: new[] { "tenant_id", "nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_documento_consumo_documento_id_ocurrido_at",
                table: "documento_consumo",
                columns: new[] { "documento_id", "ocurrido_at" });

            migrationBuilder.CreateIndex(
                name: "IX_documento_consumo_tenant_id",
                table: "documento_consumo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_consumo_version_id",
                table: "documento_consumo",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_destacados_personal_documento_id_usuario_id",
                table: "documento_destacados_personal",
                columns: new[] { "documento_id", "usuario_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documento_destacados_personal_tenant_id",
                table: "documento_destacados_personal",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_etiqueta_asignaciones_documento_id_etiqueta_catal~",
                table: "documento_etiqueta_asignaciones",
                columns: new[] { "documento_id", "etiqueta_catalogo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documento_etiqueta_asignaciones_etiqueta_catalogo_id",
                table: "documento_etiqueta_asignaciones",
                column: "etiqueta_catalogo_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_etiqueta_asignaciones_tenant_id",
                table: "documento_etiqueta_asignaciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_etiquetas_catalogo_tenant_id",
                table: "documento_etiquetas_catalogo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_etiquetas_catalogo_tenant_id_nombre",
                table: "documento_etiquetas_catalogo",
                columns: new[] { "tenant_id", "nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_documento_versiones_documento_id_numero",
                table: "documento_versiones",
                columns: new[] { "documento_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documento_versiones_tenant_id",
                table: "documento_versiones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_carpeta_id",
                table: "documentos",
                column: "carpeta_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_categoria_id",
                table: "documentos",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_tenant_id",
                table: "documentos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_documentos_tenant_id_carpeta_id",
                table: "documentos",
                columns: new[] { "tenant_id", "carpeta_id" });

            migrationBuilder.CreateIndex(
                name: "IX_documentos_tenant_id_categoria_id",
                table: "documentos",
                columns: new[] { "tenant_id", "categoria_id" });

            migrationBuilder.CreateIndex(
                name: "IX_documentos_tenant_id_estado",
                table: "documentos",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_documentos_tenant_id_origen_origen_entidad_id",
                table: "documentos",
                columns: new[] { "tenant_id", "origen", "origen_entidad_id" });

            migrationBuilder.CreateIndex(
                name: "IX_documentos_version_actual_id",
                table: "documentos",
                column: "version_actual_id");

            migrationBuilder.AddForeignKey(
                name: "FK_documento_auditoria_documentos_documento_id",
                table: "documento_auditoria",
                column: "documento_id",
                principalTable: "documentos",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_documento_consumo_documento_versiones_version_id",
                table: "documento_consumo",
                column: "version_id",
                principalTable: "documento_versiones",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_documento_consumo_documentos_documento_id",
                table: "documento_consumo",
                column: "documento_id",
                principalTable: "documentos",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_documento_destacados_personal_documentos_documento_id",
                table: "documento_destacados_personal",
                column: "documento_id",
                principalTable: "documentos",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_documento_etiqueta_asignaciones_documentos_documento_id",
                table: "documento_etiqueta_asignaciones",
                column: "documento_id",
                principalTable: "documentos",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_documento_versiones_documentos_documento_id",
                table: "documento_versiones",
                column: "documento_id",
                principalTable: "documentos",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // -----------------------------------------------------------------
            // RLS + GRANTs + triggers (Spec 2.15 v1.0)
            // -----------------------------------------------------------------

            // RLS sobre tablas con tenant_id NOT NULL (las del tenant).
            foreach (var tabla in new[] {
                "documento_carpetas",
                "documentos",
                "documento_versiones",
                "documento_etiqueta_asignaciones",
                "documento_destacados_personal",
                "documento_auditoria",
                "documento_consumo"
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

            // documento_categorias y documento_etiquetas_catalogo: tenant_id nullable
            // (base PropIA + tenant). Mismo patron que comunicado_plantillas.
            foreach (var tabla in new[] { "documento_categorias", "documento_etiquetas_catalogo" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_or_global ON {tabla}
                        USING (tenant_id IS NULL
                               OR tenant_id::text = current_setting('app.tenant_id', true))
                        WITH CHECK (tenant_id IS NULL
                                    OR tenant_id::text = current_setting('app.tenant_id', true));
                    GRANT SELECT, INSERT, UPDATE ON {tabla} TO propia_app;
                ");
            }

            // Triggers append-only (RN-15 spec 2.15) sobre documento_auditoria y documento_consumo.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION documentos_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'tabla append-only (spec 2.15). No se permite % sobre id %.', TG_OP, OLD.id;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER documento_auditoria_no_update
                    BEFORE UPDATE ON documento_auditoria
                    FOR EACH ROW EXECUTE FUNCTION documentos_append_only();

                CREATE TRIGGER documento_auditoria_no_delete
                    BEFORE DELETE ON documento_auditoria
                    FOR EACH ROW EXECUTE FUNCTION documentos_append_only();

                CREATE TRIGGER documento_consumo_no_update
                    BEFORE UPDATE ON documento_consumo
                    FOR EACH ROW EXECUTE FUNCTION documentos_append_only();

                CREATE TRIGGER documento_consumo_no_delete
                    BEFORE DELETE ON documento_consumo
                    FOR EACH ROW EXECUTE FUNCTION documentos_append_only();
            ");

            // Trigger versionado: documento_versiones es inmutable una vez creada (RN-13).
            // Solo se permite INSERT. UPDATE/DELETE bloqueados (las versiones antiguas
            // se conservan permanentemente como historial).
            migrationBuilder.Sql(@"
                CREATE TRIGGER documento_versiones_no_update
                    BEFORE UPDATE ON documento_versiones
                    FOR EACH ROW EXECUTE FUNCTION documentos_append_only();

                CREATE TRIGGER documento_versiones_no_delete
                    BEFORE DELETE ON documento_versiones
                    FOR EACH ROW EXECUTE FUNCTION documentos_append_only();
            ");

            // Seed: 9 categorias base PropIA (spec seccion 5.1, RN-12 inmutables).
            migrationBuilder.Sql(@"
                INSERT INTO documento_categorias
                    (id, tenant_id, nombre, descripcion, icono, color, es_base, activa, orden, created_at, created_by)
                VALUES
                    (gen_random_uuid(), NULL, 'Reglamentos y normativa', 'Reglamento de propiedad horizontal, manual de convivencia, RIT, RIA.', 'fi-rr-document', '#6366f1', true, true, 1, now(), NULL),
                    (gen_random_uuid(), NULL, 'Actas y asambleas', 'Actas de asamblea ordinaria, extraordinaria y consejo. Generadas por modulo 2.8.', 'fi-rr-podium', '#0ea5e9', true, true, 2, now(), NULL),
                    (gen_random_uuid(), NULL, 'Financieros', 'Estados financieros, presupuestos, ejecuciones presupuestales, certificados.', 'fi-rr-chart-pie', '#22c55e', true, true, 3, now(), NULL),
                    (gen_random_uuid(), NULL, 'Contratos y proveedores', 'Contratos vigentes, polizas, hojas de vida de proveedores.', 'fi-rr-handshake', '#f59e0b', true, true, 4, now(), NULL),
                    (gen_random_uuid(), NULL, 'Mantenimiento y operacion', 'Bitacoras, hojas de vida de equipos, certificados de inspeccion.', 'fi-rr-wrench-simple', '#a855f7', true, true, 5, now(), NULL),
                    (gen_random_uuid(), NULL, 'Comunicaciones y circulares', 'Circulares, boletines, comunicados oficiales. Archivado desde modulo 2.14.', 'fi-rr-megaphone', '#ec4899', true, true, 6, now(), NULL),
                    (gen_random_uuid(), NULL, 'PQRSD y convivencia', 'Soportes de PQRS resueltos, decisiones de comite, planes de mejora.', 'fi-rr-comment', '#14b8a6', true, true, 7, now(), NULL),
                    (gen_random_uuid(), NULL, 'Legales y juridicos', 'Demandas, paz y salvos, certificados de existencia, conceptos juridicos.', 'fi-rr-scale', '#ef4444', true, true, 8, now(), NULL),
                    (gen_random_uuid(), NULL, 'Otros', 'Documentos sin clasificacion especifica.', 'fi-rr-folder', '#64748b', true, true, 9, now(), NULL);
            ");

            // Seed: 7 etiquetas base PropIA (spec seccion 5.3).
            migrationBuilder.Sql(@"
                INSERT INTO documento_etiquetas_catalogo
                    (id, tenant_id, nombre, color, es_base, activa, created_at, created_by)
                VALUES
                    (gen_random_uuid(), NULL, 'Vigente', '#22c55e', true, true, now(), NULL),
                    (gen_random_uuid(), NULL, 'Obsoleto', '#94a3b8', true, true, now(), NULL),
                    (gen_random_uuid(), NULL, 'Urgente', '#ef4444', true, true, now(), NULL),
                    (gen_random_uuid(), NULL, 'Confidencial', '#7c3aed', true, true, now(), NULL),
                    (gen_random_uuid(), NULL, 'Publicado', '#0ea5e9', true, true, now(), NULL),
                    (gen_random_uuid(), NULL, 'Borrador', '#f59e0b', true, true, now(), NULL),
                    (gen_random_uuid(), NULL, 'Referencia', '#14b8a6', true, true, now(), NULL);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS documento_versiones_no_delete ON documento_versiones;
                DROP TRIGGER IF EXISTS documento_versiones_no_update ON documento_versiones;
                DROP TRIGGER IF EXISTS documento_consumo_no_delete ON documento_consumo;
                DROP TRIGGER IF EXISTS documento_consumo_no_update ON documento_consumo;
                DROP TRIGGER IF EXISTS documento_auditoria_no_delete ON documento_auditoria;
                DROP TRIGGER IF EXISTS documento_auditoria_no_update ON documento_auditoria;
                DROP FUNCTION IF EXISTS documentos_append_only();
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_versiones_documentos_documento_id",
                table: "documento_versiones");

            migrationBuilder.DropTable(
                name: "documento_auditoria");

            migrationBuilder.DropTable(
                name: "documento_consumo");

            migrationBuilder.DropTable(
                name: "documento_destacados_personal");

            migrationBuilder.DropTable(
                name: "documento_etiqueta_asignaciones");

            migrationBuilder.DropTable(
                name: "documento_etiquetas_catalogo");

            migrationBuilder.DropTable(
                name: "documentos");

            migrationBuilder.DropTable(
                name: "documento_carpetas");

            migrationBuilder.DropTable(
                name: "documento_versiones");

            migrationBuilder.DropTable(
                name: "documento_categorias");
        }
    }
}
