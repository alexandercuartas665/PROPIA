using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSiniestros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "siniestro_campo_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    siniestro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    siniestro_campo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_siniestro_campo_valores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "siniestro_campos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    opciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_siniestro_campos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "siniestro_estados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    clave = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_siniestro_estados", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "siniestros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    tipo_clave = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    fecha_ocurrencia = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_reporte = table.Column<DateOnly>(type: "date", nullable: true),
                    elemento_tipo = table.Column<int>(type: "integer", nullable: false),
                    elemento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    elemento_otro = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    poliza_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_reclamacion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    responsable_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    monto_estimado = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    monto_reclamado = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    deducible = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    monto_reconocido = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    fecha_pago = table.Column<DateOnly>(type: "date", nullable: true),
                    estado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_siniestros", x => x.id);
                    table.ForeignKey(
                        name: "FK_siniestros_polizas_poliza_id",
                        column: x => x.poliza_id,
                        principalTable: "polizas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_siniestros_siniestro_estados_estado_id",
                        column: x => x.estado_id,
                        principalTable: "siniestro_estados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "siniestro_comentarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    siniestro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    autor_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    autor_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    es_sistema = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_siniestro_comentarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_siniestro_comentarios_siniestros_siniestro_id",
                        column: x => x.siniestro_id,
                        principalTable: "siniestros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "siniestro_documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    siniestro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_siniestro_documentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_siniestro_documentos_siniestros_siniestro_id",
                        column: x => x.siniestro_id,
                        principalTable: "siniestros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_siniestro_campo_valores_siniestro_id_siniestro_campo_id",
                table: "siniestro_campo_valores",
                columns: new[] { "siniestro_id", "siniestro_campo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_siniestro_campo_valores_tenant_id_siniestro_id",
                table: "siniestro_campo_valores",
                columns: new[] { "tenant_id", "siniestro_id" });

            migrationBuilder.CreateIndex(
                name: "IX_siniestro_campos_tenant_id",
                table: "siniestro_campos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_siniestro_comentarios_siniestro_id",
                table: "siniestro_comentarios",
                column: "siniestro_id");

            migrationBuilder.CreateIndex(
                name: "IX_siniestro_comentarios_tenant_id_siniestro_id",
                table: "siniestro_comentarios",
                columns: new[] { "tenant_id", "siniestro_id" });

            migrationBuilder.CreateIndex(
                name: "IX_siniestro_documentos_siniestro_id",
                table: "siniestro_documentos",
                column: "siniestro_id");

            migrationBuilder.CreateIndex(
                name: "IX_siniestro_documentos_tenant_id_siniestro_id",
                table: "siniestro_documentos",
                columns: new[] { "tenant_id", "siniestro_id" });

            migrationBuilder.CreateIndex(
                name: "IX_siniestro_estados_tenant_id",
                table: "siniestro_estados",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_siniestro_estados_tenant_id_clave",
                table: "siniestro_estados",
                columns: new[] { "tenant_id", "clave" },
                unique: true,
                filter: "clave IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_siniestros_estado_id",
                table: "siniestros",
                column: "estado_id");

            migrationBuilder.CreateIndex(
                name: "IX_siniestros_poliza_id",
                table: "siniestros",
                column: "poliza_id");

            migrationBuilder.CreateIndex(
                name: "IX_siniestros_tenant_id_elemento_tipo_elemento_id",
                table: "siniestros",
                columns: new[] { "tenant_id", "elemento_tipo", "elemento_id" });

            migrationBuilder.CreateIndex(
                name: "IX_siniestros_tenant_id_estado_id",
                table: "siniestros",
                columns: new[] { "tenant_id", "estado_id" });

            migrationBuilder.CreateIndex(
                name: "IX_siniestros_tenant_id_numero",
                table: "siniestros",
                columns: new[] { "tenant_id", "numero" },
                unique: true);

            // RLS: las 6 tablas son TenantEntity. FORCE para que tambien el owner respete la policy;
            // propia_app (runtime) solo ve las filas de la copropiedad fijada en app.tenant_id.
            foreach (var tabla in TablasRls)
            {
                migrationBuilder.Sql($"ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"
CREATE POLICY tenant_isolation ON {tabla}
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());");
                migrationBuilder.Sql($"GRANT SELECT, INSERT, UPDATE, DELETE ON {tabla} TO propia_app;");
            }

            // Permisos del modulo nuevo SINIESTROS: el rol base Administrador (global) recibe las mismas
            // acciones que tiene en DOCUMENTOS (todas). Los demas roles quedan sin escritura (GET abiertos).
            migrationBuilder.Sql(@"
INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
SELECT gen_random_uuid(), rp.rol_id, 'SINIESTROS', rp.accion, rp.habilitado, rp.nivel_dato, now()
FROM rol_permisos rp
JOIN roles_copropiedad r ON r.id = rp.rol_id
WHERE r.tenant_id IS NULL AND r.nombre = 'Administrador' AND rp.modulo_codigo = 'DOCUMENTOS'
  AND NOT EXISTS (SELECT 1 FROM rol_permisos x WHERE x.rol_id = rp.rol_id AND x.modulo_codigo = 'SINIESTROS' AND x.accion = rp.accion);");
        }

        private static readonly string[] TablasRls =
        {
            "siniestro_estados", "siniestros", "siniestro_documentos",
            "siniestro_comentarios", "siniestro_campos", "siniestro_campo_valores",
        };

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM rol_permisos WHERE modulo_codigo = 'SINIESTROS';");
            migrationBuilder.Sql("DELETE FROM rol_permisos_tenant WHERE modulo_codigo = 'SINIESTROS';");

            migrationBuilder.DropTable(
                name: "siniestro_campo_valores");

            migrationBuilder.DropTable(
                name: "siniestro_campos");

            migrationBuilder.DropTable(
                name: "siniestro_comentarios");

            migrationBuilder.DropTable(
                name: "siniestro_documentos");

            migrationBuilder.DropTable(
                name: "siniestros");

            migrationBuilder.DropTable(
                name: "siniestro_estados");
        }
    }
}
