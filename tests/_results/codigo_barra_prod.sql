START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812163005_AddEquipoCodigoBarra') THEN
    ALTER TABLE equipos_activos ADD codigo_barra character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812163005_AddEquipoCodigoBarra') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260812163005_AddEquipoCodigoBarra', '9.0.19');
    END IF;
END $EF$;
COMMIT;

