using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Tres cambios que van juntos:
    ///  1. unidad_personas se vuelve polimorfico (persona natural O empresa): persona_id pasa a
    ///     nullable, se agregan empresa_id + entidad_tipo. Las filas existentes son todas personas,
    ///     asi que entidad_tipo arranca en 1 (Persona). Antiduplicado por indices PARCIALES: uno
    ///     por persona, otro por empresa (con columnas nullable, un unique compuesto no protegeria
    ///     las filas de empresa porque Postgres trata los NULL como distintos).
    ///  2. directorio_contactos se vuelve GLOBAL: se elimina la RLS tenant_isolation para que los
    ///     contactos (correos/telefonos/direcciones) viajen con la identidad y se reutilicen en
    ///     cualquier copropiedad. tenant_id queda solo como registro de quien capturo el dato.
    ///  3. Funcion buscar_empresas_organizacion (gemela de la de personas) para el autocompletado
    ///     de empresas del selector, acotado a la organizacion via SECURITY DEFINER.
    /// </summary>
    public partial class ContactosGlobalesYEmpresaEnUnidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------- 1. unidad_personas polimorfico ----------
            migrationBuilder.DropIndex(
                name: "IX_unidad_personas_tenant_id_unidad_id_persona_id_rol",
                table: "unidad_personas");

            migrationBuilder.AlterColumn<Guid>(
                name: "persona_id",
                table: "unidad_personas",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "empresa_id",
                table: "unidad_personas",
                type: "uuid",
                nullable: true);

            // defaultValue 1 = EntidadDirectorio.Persona: las filas existentes son todas personas.
            migrationBuilder.AddColumn<int>(
                name: "entidad_tipo",
                table: "unidad_personas",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_unidad_personas_tenant_id_unidad_id_empresa_id_rol",
                table: "unidad_personas",
                columns: new[] { "tenant_id", "unidad_id", "empresa_id", "rol" });

            // Antiduplicado por tipo de entidad (indices parciales).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_unidad_persona_natural " +
                "ON unidad_personas (tenant_id, unidad_id, persona_id, rol) WHERE persona_id IS NOT NULL;");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_unidad_persona_empresa " +
                "ON unidad_personas (tenant_id, unidad_id, empresa_id, rol) WHERE empresa_id IS NOT NULL;");

            // ---------- 2. directorio_contactos global (sin RLS) ----------
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON directorio_contactos;");
            migrationBuilder.Sql("ALTER TABLE directorio_contactos NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE directorio_contactos DISABLE ROW LEVEL SECURITY;");

            // ---------- 3. buscar_empresas_organizacion ----------
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION buscar_empresas_organizacion(p_tenant_id uuid, p_query text)
RETURNS TABLE (
    empresa_id uuid,
    razon_social text,
    nit text,
    tenant_id uuid,
    tenant_nombre text
)
LANGUAGE sql
STABLE
SECURITY DEFINER
SET row_security = off
AS $$
    WITH org AS (
        SELECT organizacion_id FROM tenants WHERE id = p_tenant_id
    ),
    visibles AS (
        SELECT t.id, t.nombre
        FROM tenants t, org
        WHERE (org.organizacion_id IS NOT NULL
               AND t.organizacion_id = org.organizacion_id
               AND t.estado = 1)
           OR (org.organizacion_id IS NULL AND t.id = p_tenant_id)
    )
    SELECT e.id,
           e.razon_social::text,
           e.nit::text,
           v.id,
           v.nombre::text
    FROM directorio_vinculos dv
    JOIN visibles v ON v.id = dv.tenant_id
    JOIN empresas e ON e.id = dv.entidad_id
    WHERE dv.entidad_tipo = 2      -- EntidadDirectorio.Empresa
      AND dv.estado = 1            -- EstadoVinculo.Activo
      AND (
            strpos(lower(e.razon_social), lower(p_query)) > 0
         OR strpos(lower(coalesce(e.nombre_comercial, '')), lower(p_query)) > 0
         OR strpos(lower(e.nit), lower(p_query)) > 0
      )
    LIMIT 200;
$$;
");
            migrationBuilder.Sql("REVOKE ALL ON FUNCTION buscar_empresas_organizacion(uuid, text) FROM PUBLIC;");
            migrationBuilder.Sql("GRANT EXECUTE ON FUNCTION buscar_empresas_organizacion(uuid, text) TO propia_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS buscar_empresas_organizacion(uuid, text);");

            // Reponer RLS de directorio_contactos.
            migrationBuilder.Sql("ALTER TABLE directorio_contactos ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE directorio_contactos FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "CREATE POLICY tenant_isolation ON directorio_contactos " +
                "USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_unidad_persona_empresa;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_unidad_persona_natural;");

            migrationBuilder.DropIndex(
                name: "IX_unidad_personas_tenant_id_unidad_id_empresa_id_rol",
                table: "unidad_personas");

            migrationBuilder.DropColumn(
                name: "empresa_id",
                table: "unidad_personas");

            migrationBuilder.DropColumn(
                name: "entidad_tipo",
                table: "unidad_personas");

            migrationBuilder.AlterColumn<Guid>(
                name: "persona_id",
                table: "unidad_personas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidad_personas_tenant_id_unidad_id_persona_id_rol",
                table: "unidad_personas",
                columns: new[] { "tenant_id", "unidad_id", "persona_id", "rol" },
                unique: true);
        }
    }
}
