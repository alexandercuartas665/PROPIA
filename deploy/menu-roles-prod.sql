-- PROPIA - accion manual en PROD: mover "Roles" a la seccion Configuracion del menu.
-- El menu real lo arma la tabla GLOBAL menu_overrides (sin tenant_id). En dev ya se aplico.
-- El parent_key de "Configuracion" en prod puede tener otro id (custom-sec-*): resolverlo primero.
-- Correr con el OK de Alex. Si NO se corre: "Roles" igual funciona, pero bajo "Mi Copropiedad".

-- 1) Resolver el id real de la seccion Configuracion en prod:
--    SELECT node_key, label FROM menu_overrides WHERE label ILIKE 'config%' AND is_custom = true;
--    (reemplazar <ID_CONFIG> abajo por ese node_key)

BEGIN;

UPDATE menu_overrides SET label = 'Usuarios',  updated_at = now() WHERE node_key = 'mi-usuarios';
UPDATE menu_overrides SET sort_order = 6,       updated_at = now() WHERE node_key = 'mi-directorio';

INSERT INTO menu_overrides (id, node_key, label, parent_key, sort_order, created_at, is_custom, hidden)
VALUES (gen_random_uuid(), 'mi-roles', NULL, '<ID_CONFIG>', 5, now(), false, false);

COMMIT;

-- Resultado esperado en Configuracion: Usuarios (4) -> Roles (5) -> Terceros (6).
-- (En dev el id fue custom-sec-3954c8fdc250481f9197e077ba2c6893.)

-- ROLLBACK (si hace falta revertir el movimiento):
--   BEGIN;
--   DELETE FROM menu_overrides WHERE node_key = 'mi-roles';
--   UPDATE menu_overrides SET label = 'Usuarios y roles' WHERE node_key = 'mi-usuarios';
--   -- restaurar el sort_order previo de mi-directorio si se conoce.
--   COMMIT;
