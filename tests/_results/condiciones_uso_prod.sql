START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812204537_AddEquipoCondicionesUso') THEN
    ALTER TABLE equipos_activos ADD condiciones_uso character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812204537_AddEquipoCondicionesUso') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260812204537_AddEquipoCondicionesUso', '9.0.19');
    END IF;
END $EF$;
COMMIT;

