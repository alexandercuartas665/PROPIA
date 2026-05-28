using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminListAgentsFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Funcion SECURITY DEFINER que devuelve TODOS los agentes activos con su tenant.
            // Sirve para el listado admin del Super Admin (cross-tenant) sin tener que cambiar
            // el rol propia_app a BYPASSRLS. Solo lectura.
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
                        a.id,
                        a.name::text,
                        a.role::text,
                        a.provider::text,
                        a.model::text,
                        a.sort_order,
                        a.tenant_id,
                        t.nombre::text,
                        COALESCE((SELECT COUNT(*) FROM ai_agent_mcp_tools m WHERE m.agent_id = a.id), 0)
                    FROM ai_agents a
                    JOIN tenants t ON t.id = a.tenant_id
                    WHERE a.is_active = true
                    ORDER BY t.nombre, a.sort_order, a.name;
                $$;
                GRANT EXECUTE ON FUNCTION admin_list_active_agents() TO propia_app;
            ");

            // Funcion SECURITY DEFINER que devuelve un agente especifico (con todos sus campos)
            // y sus tools MCP. Devuelve el agente como JSON para evitar definir un type complejo.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION admin_get_agent_for_template(p_agent_id uuid)
                RETURNS TABLE(
                    agent_id uuid,
                    tenant_id uuid,
                    tenant_nombre text,
                    organizacion_id uuid,
                    organizacion_nombre text,
                    name text,
                    role text,
                    provider text,
                    model text,
                    system_prompt text,
                    sort_order integer,
                    tools jsonb)
                LANGUAGE sql
                SECURITY DEFINER
                STABLE
                SET search_path = public
                AS $$
                    SELECT
                        a.id,
                        a.tenant_id,
                        t.nombre::text,
                        t.organizacion_id,
                        o.nombre::text,
                        a.name::text,
                        a.role::text,
                        a.provider::text,
                        a.model::text,
                        a.system_prompt::text,
                        a.sort_order,
                        COALESCE((
                            SELECT jsonb_agg(jsonb_build_object('connection_code', m.connection_code, 'tool_name', m.tool_name))
                            FROM ai_agent_mcp_tools m
                            WHERE m.agent_id = a.id
                        ), '[]'::jsonb)
                    FROM ai_agents a
                    JOIN tenants t ON t.id = a.tenant_id
                    LEFT JOIN organizaciones o ON o.id = t.organizacion_id
                    WHERE a.id = p_agent_id;
                $$;
                GRANT EXECUTE ON FUNCTION admin_get_agent_for_template(uuid) TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS admin_get_agent_for_template(uuid);");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS admin_list_active_agents();");
        }
    }
}
