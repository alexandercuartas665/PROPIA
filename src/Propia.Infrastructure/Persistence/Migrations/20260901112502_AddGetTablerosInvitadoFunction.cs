using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGetTablerosInvitadoFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -----------------------------------------------------------------
            // Funcion SECURITY DEFINER que devuelve los tableros de Tareas donde
            // una persona fue INVITADA (tablero_usuarios), incluyendo copros
            // donde NO tiene vinculo (usuarios_tenant): la invitacion
            // cross-tenant del tablero compartido. Mismo patron de
            // get_tenants_for_persona.
            //
            // Por que SECURITY DEFINER: tablero_usuarios y tableros tienen RLS
            // por tenant y el invitado consulta ANTES de poder entrar a ese
            // tenant. La consulta interna SOLO filtra por p_persona_id y el
            // llamador (TableroCompartidoService) garantiza que es la persona
            // del usuario autenticado del JWT.
            // -----------------------------------------------------------------
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION get_tableros_invitado(p_persona_id uuid)
RETURNS TABLE (tenant_id uuid, tenant_nombre text, tablero_id uuid, tablero_nombre text, tablero_color text, tablero_descripcion text)
LANGUAGE plpgsql
SECURITY DEFINER
SET row_security = off
AS $$
BEGIN
    RETURN QUERY
    SELECT tu.tenant_id, t.nombre::text, tb.id, tb.nombre::text, tb.color::text, tb.descripcion::text
    FROM tablero_usuarios tu
    JOIN tableros tb ON tb.id = tu.tablero_id AND tb.activo = true
    JOIN tenants t ON t.id = tu.tenant_id
    WHERE tu.persona_id = p_persona_id;
END;
$$;

-- El rol propia_app puede invocar esta funcion (no leer las tablas directas).
GRANT EXECUTE ON FUNCTION get_tableros_invitado(uuid) TO propia_app;

-- REVOKE de PUBLIC para minimizar exposicion
REVOKE EXECUTE ON FUNCTION get_tableros_invitado(uuid) FROM PUBLIC;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_tableros_invitado(uuid);");
        }
    }
}
