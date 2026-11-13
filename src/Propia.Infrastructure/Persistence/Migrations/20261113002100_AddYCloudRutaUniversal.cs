using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddYCloudRutaUniversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // URL universal del webhook de YCloud (/webhooks/ycloud, sin tenant): el webhook aun no sabe la copropiedad y,
            // con RLS forzado, propia_app no puede leer las lineas de todas. Esta funcion SECURITY DEFINER (mismo patron que
            // admin_list_active_agents) devuelve SOLO los tenant_id que tienen una linea YCloud con ese numero; la firma se
            // valida despues con el secreto de esa copropiedad. Solo lectura. Provider 3 = YCloud.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION resolver_tenants_ycloud(p_numero text)
                RETURNS TABLE(tenant_id uuid)
                LANGUAGE sql
                SECURITY DEFINER
                STABLE
                SET search_path = public
                AS $$
                    SELECT DISTINCT l.tenant_id
                    FROM whats_app_lines l
                    WHERE l.provider = 3
                      AND l.y_cloud_from_number IS NOT NULL
                      AND l.y_cloud_from_number = regexp_replace(coalesce(p_numero, ''), '[^0-9]', '', 'g');
                $$;
                REVOKE ALL ON FUNCTION resolver_tenants_ycloud(text) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION resolver_tenants_ycloud(text) TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS resolver_tenants_ycloud(text);");
        }
    }
}
