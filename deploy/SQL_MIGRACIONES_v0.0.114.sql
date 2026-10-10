-- ============================================================================
-- SQL de referencia de las migraciones del release v0.0.114 (PROPIA)
-- Fuente autoritativa: los archivos .cs en src/Propia.Infrastructure/Persistence/Migrations/
-- Se aplican con:  cd src/Propia.Api && dotnet ef database update --project ../Propia.Infrastructure --startup-project .
-- (NO ejecutar esto a mano salvo rollback/revision; ef database update ya lo corre en orden)
-- ORDEN: Directorio etiquetas -> Fase2 (aditiva) -> Fase3 (DESTRUCTIVA, dropea columna)
-- !!! BACKUP DE PROD ANTES DE APLICAR (por Fase3) !!!
-- ============================================================================


-- ----------------------------------------------------------------------------
-- 1) 20261008030714_DirectorioEtiquetasListaCatalogo  (ADITIVA)
--    Directorio: etiquetas como lista de catalogo (texto). Cambios de esquema menores/aditivos.
--    Ver el .cs para el detalle; sin riesgo de datos.
-- ----------------------------------------------------------------------------


-- ----------------------------------------------------------------------------
-- 2) 20261010101256_Fase2TareaPrioridades  (ADITIVA)
--    - AddColumn tareas.prioridad_id (uuid null) + FK a tarea_prioridades (ON DELETE SET NULL)
--    - CreateTable tarea_prioridades (por tablero) + indices + unique (tenant_id, tablero_id, nombre)
--    - RLS (FORCE + policy tenant_isolation + GRANT propia_app)
--    - Seed de las 4 prioridades de fabrica por tablero + backfill de tareas.prioridad_id
-- ----------------------------------------------------------------------------

-- RLS de la tabla nueva:
ALTER TABLE tarea_prioridades ENABLE ROW LEVEL SECURITY;
ALTER TABLE tarea_prioridades FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON tarea_prioridades
    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
GRANT SELECT, INSERT, UPDATE, DELETE ON tarea_prioridades TO propia_app;

-- Seed: 4 prioridades de fabrica en CADA tablero (es_base=true, base_valor = enum PrioridadTarea):
--   Urgente=1, Alta=2, Normal=3, Baja=4
INSERT INTO tarea_prioridades (id, tablero_id, nombre, color, orden, es_base, base_valor, activo, created_at, tenant_id)
SELECT gen_random_uuid(), t.id, v.nombre, v.color, v.orden, true, v.base_valor, true, now(), t.tenant_id
FROM tableros t
CROSS JOIN (VALUES
    ('Urgente', '#C0383C', 1, 1),
    ('Alta',    '#B45309', 2, 2),
    ('Normal',  '#2563EB', 3, 3),
    ('Baja',    '#516F90', 4, 4)
) AS v(nombre, color, orden, base_valor);

-- Backfill: cada tarea apunta a la prioridad base de SU tablero que corresponde a su enum actual.
UPDATE tareas ta
SET prioridad_id = tp.id
FROM tarea_prioridades tp
WHERE tp.tablero_id = ta.tablero_id
  AND tp.tenant_id  = ta.tenant_id
  AND tp.es_base    = true
  AND tp.base_valor = ta.prioridad;


-- ----------------------------------------------------------------------------
-- 3) 20261010205117_Fase3DropTareaPrioridadEnum  (*** DESTRUCTIVA ***)
--    Paso 0 (backfill final) -> DROP COLUMN tareas.prioridad
--    La prioridad de la tarea queda SOLO en prioridad_id -> tarea_prioridades.
-- ----------------------------------------------------------------------------

-- Paso 0.1 - por valor: opcion base del tablero cuyo base_valor == el enum guardado
UPDATE tareas ta
SET prioridad_id = tp.id
FROM tarea_prioridades tp
WHERE ta.prioridad_id IS NULL
  AND ta.tablero_id = tp.tablero_id
  AND ta.tenant_id  = tp.tenant_id
  AND tp.es_base
  AND tp.base_valor = ta.prioridad;

-- Paso 0.2 - remanentes (enum invalido/sin match, ej. 0): a la opcion Normal (base_valor=3) del tablero
UPDATE tareas ta
SET prioridad_id = tp.id
FROM tarea_prioridades tp
WHERE ta.prioridad_id IS NULL
  AND ta.tablero_id = tp.tablero_id
  AND ta.tenant_id  = tp.tenant_id
  AND tp.es_base
  AND tp.base_valor = 3;

-- Paso 1 - DROP de la columna enum (destructivo):
ALTER TABLE tareas DROP COLUMN prioridad;

-- Verificacion recomendada despues (deberia dar 0, salvo tareas huerfanas sin tablero_id):
-- SELECT count(*) FROM tareas WHERE prioridad_id IS NULL;


-- ============================================================================
-- ROLLBACK de Fase3 (Down de la migracion) - best-effort:
--   Re-crea la columna y la rellena desde la opcion (base_valor); las prioridades PERSONALIZADAS
--   no se recuperan con fidelidad (caen a Normal=3), por diseno.
-- ============================================================================
-- ALTER TABLE tareas ADD COLUMN prioridad integer NOT NULL DEFAULT 0;
-- UPDATE tareas ta SET prioridad = COALESCE(tp.base_valor, 3)
--   FROM tarea_prioridades tp WHERE ta.prioridad_id = tp.id;
-- UPDATE tareas SET prioridad = 3 WHERE prioridad = 0;
-- (equivalente a: dotnet ef database update 20261010101256_Fase2TareaPrioridades)
