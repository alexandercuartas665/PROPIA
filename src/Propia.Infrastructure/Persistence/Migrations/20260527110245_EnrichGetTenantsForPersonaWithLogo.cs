using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrichGetTenantsForPersonaWithLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enriquece la lista de copropiedades del usuario con el logo de cada PH,
            // para que el selector de copropiedad (multi-PH) muestre la miniatura real.
            // Cambia la firma de retorno -> hay que DROP antes de recrear.
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS get_tenants_for_persona(uuid);

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
      AND ut.estado = 1;  -- 1 = EstadoUsuarioTenant.Activo
END;
$$;

GRANT EXECUTE ON FUNCTION get_tenants_for_persona(uuid) TO propia_app;
REVOKE EXECUTE ON FUNCTION get_tenants_for_persona(uuid) FROM PUBLIC;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volver a la firma sin logo.
            migrationBuilder.Sql(@"
DROP FUNCTION IF EXISTS get_tenants_for_persona(uuid);

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
      AND ut.estado = 1;
END;
$$;

GRANT EXECUTE ON FUNCTION get_tenants_for_persona(uuid) TO propia_app;
REVOKE EXECUTE ON FUNCTION get_tenants_for_persona(uuid) FROM PUBLIC;
");
        }
    }
}
