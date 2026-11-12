using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Configuracion > Plantillas de documentos: catalogo central (plantillas_documento, con categoria) que
    /// reemplaza a pqrsd_plantillas_respuesta. Orden de los pasos (importa por el RLS FORCE):
    ///   1. Crear plantillas_documento SIN RLS todavia.
    ///   2. NO FORCE en la tabla vieja: el owner (que corre la migracion) deja de estar sujeto a su policy y
    ///      lee TODAS las filas de todos los tenants aunque no sea superusuario (prod).
    ///   3. Copiar las plantillas de PQRSD con categoria 'pqrsd' (mismo id, tenant, fechas).
    ///   4. Activar RLS FORCE + policy + GRANT en la tabla nueva.
    ///   5. Borrar la tabla vieja (decision de Alex: no se conserva).
    ///   6. Menu: si existe la seccion "Configuracion" del Super Admin, ubicar el item cfg-plantillas ahi,
    ///      despues de "Copropiedad" (mi-config-hub). Idempotente.
    /// </summary>
    public partial class AddPlantillasDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plantillas_documento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    categoria = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    cuerpo_html = table.Column<string>(type: "text", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plantillas_documento", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_documento_tenant_id_categoria",
                table: "plantillas_documento",
                columns: new[] { "tenant_id", "categoria" });

            // Copia de datos (antes del RLS de la tabla nueva y del DROP de la vieja).
            migrationBuilder.Sql("ALTER TABLE pqrsd_plantillas_respuesta NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
INSERT INTO plantillas_documento (id, nombre, categoria, cuerpo_html, orden, created_at, created_by, updated_at, updated_by, tenant_id)
SELECT id, nombre, 'pqrsd', cuerpo_html, orden, created_at, created_by, updated_at, updated_by, tenant_id
FROM pqrsd_plantillas_respuesta
WHERE activa
ON CONFLICT (id) DO NOTHING;");

            migrationBuilder.Sql("ALTER TABLE plantillas_documento ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE plantillas_documento FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY tenant_isolation ON plantillas_documento
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON plantillas_documento TO propia_app;");

            migrationBuilder.DropTable(name: "pqrsd_plantillas_respuesta");

            // Menu: item en la seccion "Configuracion" (custom del Super Admin) despues de "Copropiedad".
            // Mismo sort_order que mi-config-hub: el desempate por etiqueta deja "Copropiedad" antes.
            migrationBuilder.Sql(@"
INSERT INTO menu_overrides (id, node_key, parent_key, sort_order, created_at, is_custom, hidden)
SELECT gen_random_uuid(), 'cfg-plantillas', s.node_key,
       COALESCE((SELECT h.sort_order FROM menu_overrides h WHERE h.node_key = 'mi-config-hub' AND h.parent_key = s.node_key), 3),
       now(), false, false
FROM menu_overrides s
WHERE s.node_type = 'section' AND lower(s.label) IN ('configuración', 'configuracion')
  AND NOT EXISTS (SELECT 1 FROM menu_overrides x WHERE x.node_key = 'cfg-plantillas')
ORDER BY s.sort_order NULLS LAST
LIMIT 1;");

            // Permisos del modulo nuevo PLANTILLAS: el rol base Administrador (global) tiene TODAS las acciones
            // de todos los modulos; se le copia su fila de DOCUMENTOS (6 acciones, habilitado, nivel copropiedad).
            // Los demas roles base quedan sin acceso de escritura (los GET son abiertos al tenant).
            migrationBuilder.Sql(@"
INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
SELECT gen_random_uuid(), rp.rol_id, 'PLANTILLAS', rp.accion, rp.habilitado, rp.nivel_dato, now()
FROM rol_permisos rp
JOIN roles_copropiedad r ON r.id = rp.rol_id
WHERE r.tenant_id IS NULL AND r.nombre = 'Administrador' AND rp.modulo_codigo = 'DOCUMENTOS'
  AND NOT EXISTS (SELECT 1 FROM rol_permisos x WHERE x.rol_id = rp.rol_id AND x.modulo_codigo = 'PLANTILLAS' AND x.accion = rp.accion);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_overrides WHERE node_key = 'cfg-plantillas';");
            migrationBuilder.Sql("DELETE FROM rol_permisos WHERE modulo_codigo = 'PLANTILLAS';");
            migrationBuilder.Sql("DELETE FROM rol_permisos_tenant WHERE modulo_codigo = 'PLANTILLAS';");

            migrationBuilder.CreateTable(
                name: "pqrsd_plantillas_respuesta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cuerpo_html = table.Column<string>(type: "text", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_plantillas_respuesta", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_plantillas_respuesta_tenant_id",
                table: "pqrsd_plantillas_respuesta",
                column: "tenant_id");

            // Devolver las de PQRSD a la tabla vieja (las de otras categorias no tienen donde ir).
            migrationBuilder.Sql("ALTER TABLE plantillas_documento NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
INSERT INTO pqrsd_plantillas_respuesta (id, activa, created_at, created_by, cuerpo_html, nombre, orden, tenant_id, updated_at, updated_by)
SELECT id, true, created_at, created_by, cuerpo_html, nombre, orden, tenant_id, updated_at, updated_by
FROM plantillas_documento WHERE categoria = 'pqrsd';");

            migrationBuilder.Sql("ALTER TABLE pqrsd_plantillas_respuesta ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE pqrsd_plantillas_respuesta FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY tenant_isolation ON pqrsd_plantillas_respuesta
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_plantillas_respuesta TO propia_app;");

            migrationBuilder.DropTable(name: "plantillas_documento");
        }
    }
}
