using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Autocompletado de personas a nivel ORGANIZACION para el selector de personas.
    ///
    /// Por que hace falta una funcion SECURITY DEFINER y no basta con EF:
    /// directorio_vinculos esta bajo RLS y el rol de la aplicacion NO tiene BYPASSRLS
    /// (verificado en la BD: a propia_app se le deniega 'SET row_security = off').
    /// IgnoreQueryFilters() de EF solo quita el filtro de C#; la policy de Postgres sigue
    /// recortando a la copropiedad activa, asi que una consulta cross-copropiedad desde EF
    /// devuelve vacio siempre.
    ///
    /// CONTENCION DE ESTA FUNCION - leer antes de tocarla:
    ///  1. p_tenant_id lo pone el servidor desde el JWT. NUNCA aceptarlo del cliente: si se
    ///     acepta, esto se vuelve un enumerador de toda la plataforma.
    ///  2. El alcance se deriva DENTRO de la funcion (la organizacion del tenant recibido),
    ///     no se recibe una lista de tenants. Asi el llamador no puede ampliarlo.
    ///  3. Si la copropiedad no tiene organizacion, el alcance se reduce a ella misma. El
    ///     caso degradado nunca amplia.
    ///  4. Devuelve solo lo necesario para elegir a alguien: nombre y documento. Sin email
    ///     ni telefono, porque el resultado puede incluir gente de otra copropiedad.
    ///
    /// Mismo molde que get_tenants_for_persona (login), incluido el GRANT/REVOKE.
    /// </summary>
    public partial class AddBuscarPersonasOrganizacionFunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION buscar_personas_organizacion(p_tenant_id uuid, p_query text)
RETURNS TABLE (
    persona_id uuid,
    nombres text,
    apellidos text,
    tipo_documento integer,
    documento text,
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
        -- Copropiedades activas de la misma organizacion. Si el tenant no tiene
        -- organizacion, el alcance se limita a el mismo (nunca se amplia).
        SELECT t.id, t.nombre
        FROM tenants t, org
        WHERE (org.organizacion_id IS NOT NULL
               AND t.organizacion_id = org.organizacion_id
               AND t.estado = 1)
           OR (org.organizacion_id IS NULL AND t.id = p_tenant_id)
    )
    SELECT p.id,
           p.nombres::text,
           p.apellidos::text,
           p.tipo_documento,
           p.documento::text,
           v.id,
           v.nombre::text
    FROM directorio_vinculos dv
    JOIN visibles v ON v.id = dv.tenant_id
    JOIN personas p ON p.id = dv.entidad_id
    WHERE dv.entidad_tipo = 1      -- EntidadDirectorio.Persona
      AND dv.estado = 1            -- EstadoVinculo.Activo
      AND (
            strpos(lower(p.nombres || ' ' || p.apellidos), lower(p_query)) > 0
         OR strpos(lower(p.documento), lower(p_query)) > 0
      )
    LIMIT 200;
$$;
");

            // Solo el rol de la aplicacion puede ejecutarla.
            migrationBuilder.Sql("REVOKE ALL ON FUNCTION buscar_personas_organizacion(uuid, text) FROM PUBLIC;");
            migrationBuilder.Sql("GRANT EXECUTE ON FUNCTION buscar_personas_organizacion(uuid, text) TO propia_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS buscar_personas_organizacion(uuid, text);");
        }
    }
}
