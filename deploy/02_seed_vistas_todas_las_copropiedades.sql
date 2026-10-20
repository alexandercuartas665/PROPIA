-- ============================================================================
-- PROPIA v0.0.206 - Siembra de "Vistas del sistema" (widgets) en TODAS las copropiedades
-- ----------------------------------------------------------------------------
-- Inserta las 8 vistas/widgets de ejemplo en cada copropiedad (tenant) que AUN NO tenga vistas.
--
-- IDEMPOTENTE: re-ejecutable. Solo siembra donde no hay vistas (igual que el primer arranque del
--   panel, reemplazar=false). A las copropiedades que ya tengan vistas (p.ej. sembradas desde la
--   UI) NO las toca.
-- RLS: vistas_sistema tiene Row-Level Security (FORCE). Este script fija app.tenant_id por tenant
--   para que la policy tenant_isolation permita el INSERT (tenant_id = current_tenant_id()).
--
-- CORRER: como el rol dueno de la BD (propia), DESPUES de aplicar la migracion AddVistasSistema
--   (que crea la tabla vistas_sistema + su RLS). Ver el handoff.
--     psql "<CONNECTION_STRING_PROPIA>" -f 02_seed_vistas_todas_las_copropiedades.sql
-- ============================================================================
SET client_encoding = 'UTF8';

DO $$
DECLARE
    t RECORD;
    n_sembradas int := 0;
    n_omitidas  int := 0;
BEGIN
    -- tenants es una tabla GLOBAL (sin RLS): lista todas las copropiedades.
    FOR t IN SELECT id FROM tenants LOOP
        -- Fija el tenant de la sesion (transaccion-local) para pasar la policy de RLS.
        PERFORM set_config('app.tenant_id', t.id::text, true);

        IF EXISTS (SELECT 1 FROM vistas_sistema WHERE tenant_id = t.id) THEN
            n_omitidas := n_omitidas + 1;
            CONTINUE;  -- ya tiene vistas: no se toca
        END IF;

        INSERT INTO vistas_sistema (id, nombre, orden, definicion, created_at, tenant_id) VALUES
          (gen_random_uuid(), 'Unidades por tipo', 0,
           '{"kind": "chart", "chart": "bars", "dsKey": "unidades", "title": "", "width": "half", "colDim": "", "fields": [], "rowDim": "tipo", "filters": [], "measure": {"f": null, "fmt": null, "type": "count", "label": null}}'::jsonb,
           now(), t.id),
          (gen_random_uuid(), 'Cartera por estado de gestión', 1,
           '{"kind": "pivot", "chart": "bars", "dsKey": "cartera", "title": "", "width": "half", "colDim": "", "fields": [], "rowDim": "estado", "filters": [], "measure": {"f": "saldo", "fmt": "money", "type": "sum", "label": "Saldo total"}}'::jsonb,
           now(), t.id),
          (gen_random_uuid(), 'PQRSD por estado', 2,
           '{"kind": "chart", "chart": "donut", "dsKey": "pqrsd", "title": "", "width": "third", "colDim": "", "fields": [], "rowDim": "estado", "filters": [], "measure": {"f": null, "fmt": null, "type": "count", "label": null}}'::jsonb,
           now(), t.id),
          (gen_random_uuid(), 'Tareas por etapa', 3,
           '{"kind": "pivot", "chart": "bars", "dsKey": "tareas", "title": "", "width": "third", "colDim": "", "fields": [], "rowDim": "etapa", "filters": [], "measure": {"f": null, "fmt": null, "type": "count", "label": null}}'::jsonb,
           now(), t.id),
          (gen_random_uuid(), 'Directorio de residentes', 4,
           '{"kind": "table", "chart": "bars", "dsKey": "residentes", "title": "", "width": "third", "colDim": "", "fields": ["relacion", "unidad", "celular", "email", "estado"], "rowDim": "", "filters": [], "measure": {"f": null, "fmt": null, "type": "count", "label": null}}'::jsonb,
           now(), t.id),
          (gen_random_uuid(), 'Unidades por tipo y estado', 5,
           '{"kind": "pivot", "chart": "bars", "dsKey": "unidades", "title": "", "width": "full", "colDim": "estado", "fields": [], "rowDim": "tipo", "filters": [], "measure": {"f": null, "fmt": null, "type": "count", "label": null}}'::jsonb,
           now(), t.id),
          (gen_random_uuid(), 'Contratos por categoría (valor mensual)', 6,
           '{"kind": "chart", "chart": "bars", "dsKey": "contratos", "title": "", "width": "half", "colDim": "", "fields": [], "rowDim": "categoria", "filters": [], "measure": {"f": "valormensual", "fmt": "money", "type": "sum", "label": "Valor mensual"}}'::jsonb,
           now(), t.id),
          (gen_random_uuid(), 'Zonas comunes', 7,
           '{"kind": "cards", "chart": "bars", "dsKey": "zonas", "title": "", "width": "half", "colDim": "", "fields": ["tipo", "estado", "aforo"], "rowDim": "", "filters": [], "measure": {"f": null, "fmt": null, "type": "count", "label": null}}'::jsonb,
           now(), t.id);

        n_sembradas := n_sembradas + 1;
    END LOOP;

    -- Limpia el tenant de la sesion (por si el cliente reutiliza la conexion).
    PERFORM set_config('app.tenant_id', '', true);

    RAISE NOTICE 'Vistas sembradas: % copropiedades nuevas, % ya tenian vistas (omitidas).', n_sembradas, n_omitidas;
END $$;

-- Verificacion (opcional): cuantas copropiedades tienen las 8 vistas.
-- NOTA: este SELECT corre bajo RLS; para contar GLOBAL hazlo como superusuario o con el app.tenant_id
-- por tenant. Alternativa rapida como dueno con BYPASSRLS/superuser:
--   SELECT count(DISTINCT tenant_id) AS copropiedades_con_vistas, count(*) AS filas FROM vistas_sistema;
