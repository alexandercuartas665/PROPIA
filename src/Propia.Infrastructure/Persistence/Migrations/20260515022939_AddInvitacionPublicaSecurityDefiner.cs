using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitacionPublicaSecurityDefiner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El endpoint publico /api/invitaciones/{token} debe poder consultar una invitacion
            // sin estar autenticado (sin tenant en JWT). Con RLS estricto la policy bloquea cualquier
            // SELECT cuando current_tenant_id() es NULL. Solucion: una funcion SECURITY DEFINER que
            // recupera SOLO los campos necesarios para mostrar la pantalla de aceptacion - el secreto
            // ya esta en el token (48 bytes aleatorios) que solo conoce quien recibio el email.
            //
            // Lo mismo aplica al endpoint POST /api/invitaciones/aceptar que necesita leer + actualizar
            // la invitacion. Para ese flujo cambiamos a usar la funcion para leer, y luego ejecutamos
            // un UPDATE con SET LOCAL app.tenant_id en la misma transaccion (RLS WITH CHECK se cumple).

            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS get_invitacion_publica(text);
                CREATE FUNCTION get_invitacion_publica(p_token text)
                RETURNS TABLE (
                    id uuid,
                    tenant_id uuid,
                    persona_id uuid,
                    rol_id uuid,
                    estado integer,
                    expira_at timestamptz,
                    canal_envio integer,
                    created_at timestamptz,
                    persona_nombres varchar,
                    persona_apellidos varchar,
                    persona_email varchar,
                    rol_nombre varchar,
                    copropiedad_nombre varchar
                )
                LANGUAGE sql
                SECURITY DEFINER
                SET search_path = public
                AS $$
                    SELECT
                        i.id, i.tenant_id, i.persona_id, i.rol_id,
                        i.estado, i.expira_at, i.canal_envio, i.created_at,
                        p.nombres, p.apellidos, p.email,
                        r.nombre, t.nombre
                    FROM usuario_invitaciones i
                    JOIN personas p ON p.id = i.persona_id
                    JOIN roles_copropiedad r ON r.id = i.rol_id
                    JOIN tenants t ON t.id = i.tenant_id
                    WHERE i.token = p_token
                    LIMIT 1
                $$;

                GRANT EXECUTE ON FUNCTION get_invitacion_publica(text) TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_invitacion_publica(text);");
        }
    }
}
