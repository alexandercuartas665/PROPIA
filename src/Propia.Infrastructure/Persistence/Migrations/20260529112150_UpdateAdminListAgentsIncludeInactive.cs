using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminListAgentsIncludeInactive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reemplaza admin_list_active_agents() para devolver TODOS los agentes (activos e
            // inactivos) con la columna is_active. Asi el modal "Importar de tenant" del Super
            // Admin puede importar tambien agentes que el tenant tiene apagados (con un badge
            // claro en UI). El filtro is_active queda como decision del cliente UI.
            // DROP requerido porque cambia el RETURN TABLE shape (anadimos is_active).
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS admin_list_active_agents();");
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION admin_list_active_agents()
                RETURNS TABLE(
                    agent_id uuid,
                    agent_name text,
                    role text,
                    provider text,
                    model text,
                    sort_order integer,
                    tenant_id uuid,
                    tenant_nombre text,
                    tools_count bigint,
                    is_active boolean)
                LANGUAGE sql
                SECURITY DEFINER
                STABLE
                SET search_path = public
                AS $$
                    SELECT
                        a.id,
                        a.name::text,
                        a.role::text,
                        a.provider::text,
                        a.model::text,
                        a.sort_order,
                        a.tenant_id,
                        t.nombre::text,
                        COALESCE((SELECT COUNT(*) FROM ai_agent_mcp_tools m WHERE m.agent_id = a.id), 0),
                        a.is_active
                    FROM ai_agents a
                    JOIN tenants t ON t.id = a.tenant_id
                    ORDER BY t.nombre, a.is_active DESC, a.sort_order, a.name;
                $$;
                GRANT EXECUTE ON FUNCTION admin_list_active_agents() TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura version anterior (solo activos)
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS admin_list_active_agents();");
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION admin_list_active_agents()
                RETURNS TABLE(
                    agent_id uuid,
                    agent_name text,
                    role text,
                    provider text,
                    model text,
                    sort_order integer,
                    tenant_id uuid,
                    tenant_nombre text,
                    tools_count bigint)
                LANGUAGE sql
                SECURITY DEFINER
                STABLE
                SET search_path = public
                AS $$
                    SELECT
                        a.id, a.name::text, a.role::text, a.provider::text, a.model::text,
                        a.sort_order, a.tenant_id, t.nombre::text,
                        COALESCE((SELECT COUNT(*) FROM ai_agent_mcp_tools m WHERE m.agent_id = a.id), 0)
                    FROM ai_agents a
                    JOIN tenants t ON t.id = a.tenant_id
                    WHERE a.is_active = true
                    ORDER BY t.nombre, a.sort_order, a.name;
                $$;
                GRANT EXECUTE ON FUNCTION admin_list_active_agents() TO propia_app;
            ");
        }
    }
}
