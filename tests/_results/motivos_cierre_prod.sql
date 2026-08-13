START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812212129_AddMotivosCierre') THEN
    ALTER TABLE tareas ADD cerrada boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812212129_AddMotivosCierre') THEN
    ALTER TABLE tareas ADD cerrada_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812212129_AddMotivosCierre') THEN
    ALTER TABLE tareas ADD motivo_cierre_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812212129_AddMotivosCierre') THEN
    ALTER TABLE pqrsd_expedientes ADD motivo_cierre_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812212129_AddMotivosCierre') THEN
    CREATE TABLE motivos_cierre (
        id uuid NOT NULL,
        modulo character varying(20) NOT NULL,
        nombre character varying(120) NOT NULL,
        clasificacion integer NOT NULL,
        es_base boolean NOT NULL,
        activo boolean NOT NULL DEFAULT TRUE,
        orden integer NOT NULL,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        updated_at timestamp with time zone,
        updated_by uuid,
        tenant_id uuid NOT NULL,
        CONSTRAINT "PK_motivos_cierre" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812212129_AddMotivosCierre') THEN
    CREATE INDEX "IX_motivos_cierre_tenant_id" ON motivos_cierre (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812212129_AddMotivosCierre') THEN
    CREATE UNIQUE INDEX "IX_motivos_cierre_tenant_id_modulo_nombre" ON motivos_cierre (tenant_id, modulo, nombre);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812212129_AddMotivosCierre') THEN

                    ALTER TABLE motivos_cierre ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE motivos_cierre FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON motivos_cierre
                        USING (tenant_id = current_tenant_id())
                        WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON motivos_cierre TO propia_app;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812212129_AddMotivosCierre') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260812212129_AddMotivosCierre', '9.0.19');
    END IF;
END $EF$;
COMMIT;

