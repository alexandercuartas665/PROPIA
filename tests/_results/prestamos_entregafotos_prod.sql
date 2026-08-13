START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    ALTER TABLE reservas ADD devolucion_observacion text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    ALTER TABLE reservas ADD devuelta_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    ALTER TABLE reservas ADD devuelta_por_persona_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    ALTER TABLE reservas ADD entrega_observacion text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    ALTER TABLE reservas ADD entregada_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    ALTER TABLE reservas ADD entregada_por_persona_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    CREATE TABLE entrega_fotos (
        id uuid NOT NULL,
        origen_tipo character varying(20) NOT NULL,
        origen_id uuid NOT NULL,
        url character varying(1024) NOT NULL,
        momento integer NOT NULL,
        registrado_por_persona_id uuid,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        updated_at timestamp with time zone,
        updated_by uuid,
        tenant_id uuid NOT NULL,
        CONSTRAINT "PK_entrega_fotos" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    CREATE TABLE prestamos_equipo (
        id uuid NOT NULL,
        codigo character varying(30) NOT NULL,
        equipo_activo_id uuid NOT NULL,
        persona_id uuid NOT NULL,
        unidad_privada_id uuid,
        fecha date NOT NULL,
        hora_inicio time without time zone,
        hora_fin time without time zone,
        estado integer NOT NULL,
        observaciones text,
        entregado_at timestamp with time zone,
        entregado_por_persona_id uuid,
        entrega_observacion text,
        devuelto_at timestamp with time zone,
        devuelto_por_persona_id uuid,
        devolucion_observacion text,
        motivo_cancelacion text,
        created_at timestamp with time zone NOT NULL,
        created_by uuid,
        updated_at timestamp with time zone,
        updated_by uuid,
        tenant_id uuid NOT NULL,
        CONSTRAINT "PK_prestamos_equipo" PRIMARY KEY (id),
        CONSTRAINT "FK_prestamos_equipo_equipos_activos_equipo_activo_id" FOREIGN KEY (equipo_activo_id) REFERENCES equipos_activos (id) ON DELETE CASCADE,
        CONSTRAINT "FK_prestamos_equipo_personas_persona_id" FOREIGN KEY (persona_id) REFERENCES personas (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_prestamos_equipo_unidades_privadas_unidad_privada_id" FOREIGN KEY (unidad_privada_id) REFERENCES unidades_privadas (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    CREATE INDEX "IX_entrega_fotos_tenant_id" ON entrega_fotos (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    CREATE INDEX "IX_entrega_fotos_tenant_id_origen_tipo_origen_id" ON entrega_fotos (tenant_id, origen_tipo, origen_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    CREATE INDEX "IX_prestamos_equipo_equipo_activo_id" ON prestamos_equipo (equipo_activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    CREATE INDEX "IX_prestamos_equipo_persona_id" ON prestamos_equipo (persona_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    CREATE INDEX "IX_prestamos_equipo_tenant_id" ON prestamos_equipo (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    CREATE UNIQUE INDEX "IX_prestamos_equipo_tenant_id_codigo" ON prestamos_equipo (tenant_id, codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    CREATE INDEX "IX_prestamos_equipo_tenant_id_equipo_activo_id_fecha" ON prestamos_equipo (tenant_id, equipo_activo_id, fecha);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    CREATE INDEX "IX_prestamos_equipo_unidad_privada_id" ON prestamos_equipo (unidad_privada_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN

                    ALTER TABLE prestamos_equipo ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE prestamos_equipo FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON prestamos_equipo
                        USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON prestamos_equipo TO propia_app;

                    ALTER TABLE entrega_fotos ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE entrega_fotos FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON entrega_fotos
                        USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON entrega_fotos TO propia_app;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260812152713_AddPrestamosEquipoYEntregaFotos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260812152713_AddPrestamosEquipoYEntregaFotos', '9.0.19');
    END IF;
END $EF$;
COMMIT;

