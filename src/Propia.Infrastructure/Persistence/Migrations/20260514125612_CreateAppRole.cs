using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class CreateAppRole : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ---------------------------------------------------------------------
        // Crear rol propia_app: usuario que la aplicacion .NET usa en runtime.
        // NO es superuser, NO tiene BYPASSRLS - se ve afectado por las policies
        // de RLS. El rol propia (POSTGRES_USER del docker-compose) sigue siendo
        // superuser para migraciones y administracion.
        //
        // En produccion el password se rota via variable de entorno y se gestiona
        // como secret. Aqui (dev) un default conocido para que la connection
        // string del Api funcione sin intervencion manual.
        // ---------------------------------------------------------------------
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'propia_app') THEN
        CREATE ROLE propia_app WITH LOGIN PASSWORD 'PropiaAppDev2026!' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    END IF;
END $$;

-- Permisos sobre el schema public
GRANT USAGE ON SCHEMA public TO propia_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO propia_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO propia_app;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO propia_app;

-- Permisos por defecto sobre futuras tablas creadas por el owner
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO propia_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO propia_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT EXECUTE ON FUNCTIONS TO propia_app;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
REVOKE ALL ON SCHEMA public FROM propia_app;
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM propia_app;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM propia_app;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA public FROM propia_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON TABLES FROM propia_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON SEQUENCES FROM propia_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ALL ON FUNCTIONS FROM propia_app;
DROP ROLE IF EXISTS propia_app;
");
    }
}
