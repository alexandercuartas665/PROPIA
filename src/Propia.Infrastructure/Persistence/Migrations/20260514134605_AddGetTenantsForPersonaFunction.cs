using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddGetTenantsForPersonaFunction : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ---------------------------------------------------------------------
        // Funcion SECURITY DEFINER que devuelve las copropiedades a las que
        // tiene acceso una persona.
        //
        // Por que SECURITY DEFINER: el endpoint /connect/token (login) corre
        // ANTES de que el usuario tenga un tenant activo. RLS bloquea leer
        // usuarios_tenant en ese momento. Esta funcion corre con los permisos
        // del owner y desactiva row_security para la sesion - puede leer
        // cross-tenant pero la consulta interna SOLO filtra por p_persona_id.
        //
        // Seguridad: la funcion recibe persona_id como argumento y SOLO retorna
        // las filas de esa persona. El llamador (AuthService) garantiza que el
        // persona_id es el del usuario autenticado del JWT.
        // ---------------------------------------------------------------------
        migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION get_tenants_for_persona(p_persona_id uuid)
RETURNS TABLE (tenant_id uuid, nombre text, rol text)
LANGUAGE plpgsql
SECURITY DEFINER
SET row_security = off
AS $$
BEGIN
    RETURN QUERY
    SELECT ut.tenant_id, t.nombre::text, ut.rol::text
    FROM usuarios_tenant ut
    JOIN tenants t ON t.id = ut.tenant_id
    WHERE ut.persona_id = p_persona_id
      AND ut.estado = 1;  -- 1 = EstadoUsuarioTenant.Activo
END;
$$;

-- El rol propia_app puede invocar esta funcion (no leer la tabla directa).
GRANT EXECUTE ON FUNCTION get_tenants_for_persona(uuid) TO propia_app;

-- REVOKE de PUBLIC para minimizar exposicion
REVOKE EXECUTE ON FUNCTION get_tenants_for_persona(uuid) FROM PUBLIC;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_tenants_for_persona(uuid);");
    }
}
