using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioTenantArchivado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preferencia POR USUARIO: oculta la copropiedad de su selector (no cambia el estado global).
            migrationBuilder.AddColumn<bool>(
                name: "archivado",
                table: "usuarios_tenant",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Recrear get_tenants_for_persona para devolver tambien 'archivado' y 'ultimo_acceso' (los usa el
            // selector para separar activas/archivadas y mostrar el ultimo ingreso). Cambia la firma -> DROP+CREATE.
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS get_tenants_for_persona(uuid);

CREATE OR REPLACE FUNCTION get_tenants_for_persona(p_persona_id uuid)
RETURNS TABLE (tenant_id uuid, nombre text, rol text, logo_url text, archivado boolean, ultimo_acceso timestamptz)
LANGUAGE plpgsql
SECURITY DEFINER
SET row_security = off
AS $$
BEGIN
    RETURN QUERY
    SELECT ut.tenant_id, t.nombre::text, ut.rol::text, t.logo_url::text, ut.archivado, ut.ultimo_acceso
    FROM usuarios_tenant ut
    JOIN tenants t ON t.id = ut.tenant_id
    WHERE ut.persona_id = p_persona_id
      AND ut.estado = 1;  -- 1 = EstadoUsuarioTenant.Activo
END;
$$;

GRANT EXECUTE ON FUNCTION get_tenants_for_persona(uuid) TO propia_app;
REVOKE EXECUTE ON FUNCTION get_tenants_for_persona(uuid) FROM PUBLIC;

-- Archiva/desarchiva el vinculo de UNA persona con UN tenant, saltando la RLS de usuarios_tenant
-- (SECURITY DEFINER + row_security off). El servicio solo pasa la persona del usuario autenticado,
-- asi que un usuario solo puede archivar sus propios vinculos. Devuelve filas afectadas.
CREATE OR REPLACE FUNCTION set_usuario_tenant_archivado(p_persona_id uuid, p_tenant_id uuid, p_archivado boolean)
RETURNS integer
LANGUAGE plpgsql
SECURITY DEFINER
SET row_security = off
AS $$
DECLARE n integer;
BEGIN
    UPDATE usuarios_tenant
       SET archivado = p_archivado
     WHERE persona_id = p_persona_id AND tenant_id = p_tenant_id;
    GET DIAGNOSTICS n = ROW_COUNT;
    RETURN n;
END;
$$;

GRANT EXECUTE ON FUNCTION set_usuario_tenant_archivado(uuid, uuid, boolean) TO propia_app;
REVOKE EXECUTE ON FUNCTION set_usuario_tenant_archivado(uuid, uuid, boolean) FROM PUBLIC;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS set_usuario_tenant_archivado(uuid, uuid, boolean);
DROP FUNCTION IF EXISTS get_tenants_for_persona(uuid);

-- Volver a la firma anterior (sin archivado/ultimo_acceso).
CREATE OR REPLACE FUNCTION get_tenants_for_persona(p_persona_id uuid)
RETURNS TABLE (tenant_id uuid, nombre text, rol text, logo_url text)
LANGUAGE plpgsql
SECURITY DEFINER
SET row_security = off
AS $$
BEGIN
    RETURN QUERY
    SELECT ut.tenant_id, t.nombre::text, ut.rol::text, t.logo_url::text
    FROM usuarios_tenant ut
    JOIN tenants t ON t.id = ut.tenant_id
    WHERE ut.persona_id = p_persona_id
      AND ut.estado = 1;
END;
$$;

GRANT EXECUTE ON FUNCTION get_tenants_for_persona(uuid) TO propia_app;
REVOKE EXECUTE ON FUNCTION get_tenants_for_persona(uuid) FROM PUBLIC;
");

            migrationBuilder.DropColumn(
                name: "archivado",
                table: "usuarios_tenant");
        }
    }
}
