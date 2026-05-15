-- ============================================================================
-- Bootstrap del rol runtime para PROPIA en Railway PostgreSQL.
--
-- Ejecutar UNA VEZ contra la base recien creada del plugin Railway, ANTES de
-- aplicar las migraciones EF. Despues de esto, el role propia_app existe con
-- el password aleatorio fuerte que pasamos como variable.
--
-- Uso:
--   PROPIA_APP_PASSWORD=$(openssl rand -hex 24)
--   psql "$DATABASE_URL" -v propia_app_pwd="'$PROPIA_APP_PASSWORD'" -f init-roles.sql
--
-- Verificacion:
--   psql "$DATABASE_URL" -c "SELECT rolname, rolsuper, rolbypassrls FROM pg_roles WHERE rolname='propia_app';"
--   Debe devolver: propia_app | f | f
--
-- La migracion existente 20260514125612_CreateAppRole.cs es idempotente
-- (IF NOT EXISTS) y NO pisa el password aplicado aqui.
-- ============================================================================

\set ON_ERROR_STOP on

-- Extensiones requeridas por las migraciones existentes.
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
CREATE EXTENSION IF NOT EXISTS "citext";

-- Crear o actualizar el rol propia_app con NOSUPERUSER NOBYPASSRLS.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'propia_app') THEN
        EXECUTE format(
            'CREATE ROLE propia_app WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS',
            :'propia_app_pwd'
        );
    ELSE
        EXECUTE format(
            'ALTER ROLE propia_app WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS',
            :'propia_app_pwd'
        );
    END IF;
END
$$;

-- Confirmacion rapida
SELECT rolname, rolsuper, rolbypassrls, rolcanlogin
FROM pg_roles
WHERE rolname = 'propia_app';
