-- ============================================================================
-- PROPIA v0.0.206 - Migracion AddVistasSistema (equivalente SQL)
-- ----------------------------------------------------------------------------
-- Crea la tabla vistas_sistema (donde se guardan las "Vistas del sistema" / widgets, COMPARTIDAS
-- por copropiedad) con su Row-Level Security por tenant.
--
-- RECOMENDADO: aplicar con EF, que maneja el historial automaticamente:
--     cd src/Propia.Api
--     "$USERPROFILE/.dotnet/tools/dotnet-ef.exe" database update --project ../Propia.Infrastructure --startup-project .
--   (`database update` aplica SOLO lo que falte; es aditiva y reversible.)
--
-- Este .sql es la ALTERNATIVA MANUAL (si se aplica por fuera de EF). Corre como el rol dueno (propia).
-- La ultima sentencia registra la migracion en __EFMigrationsHistory para que EF no la re-aplique.
-- ============================================================================
BEGIN;

CREATE TABLE vistas_sistema (
    id          uuid NOT NULL,
    nombre      character varying(200) NOT NULL,
    orden       integer NOT NULL,
    definicion  jsonb NOT NULL,
    created_at  timestamp with time zone NOT NULL,
    created_by  uuid NULL,
    updated_at  timestamp with time zone NULL,
    updated_by  uuid NULL,
    tenant_id   uuid NOT NULL,
    CONSTRAINT "PK_vistas_sistema" PRIMARY KEY (id)
);

CREATE INDEX "IX_vistas_sistema_tenant_id"       ON vistas_sistema (tenant_id);
CREATE INDEX "IX_vistas_sistema_tenant_id_orden" ON vistas_sistema (tenant_id, orden);

-- RLS + FORCE + policy tenant_isolation + GRANT (mismo patron que el resto de Capa 2;
-- lo exige RlsCoverageTests). current_tenant_id() lee app.tenant_id de la sesion.
ALTER TABLE vistas_sistema ENABLE ROW LEVEL SECURITY;
ALTER TABLE vistas_sistema FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON vistas_sistema
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());
GRANT SELECT, INSERT, UPDATE, DELETE ON vistas_sistema TO propia_app;

-- Historial de EF (solo necesario si se aplica por fuera de `dotnet ef`).
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261020022307_AddVistasSistema', '9.0.20')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
