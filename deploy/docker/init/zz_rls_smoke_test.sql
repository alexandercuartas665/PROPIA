-- Smoke test RLS usando rol propia_app (no superuser, no bypassrls).
-- Ejecutar: docker exec -i propia-postgres psql -U propia -d propia_dev < zz_rls_smoke_test.sql
\set ON_ERROR_STOP on

\echo '---- Confirmando que propia_app NO es superuser ni bypassrls ----'
SELECT rolname, rolsuper, rolbypassrls FROM pg_roles WHERE rolname = 'propia_app';

\echo '---- Setup datos de prueba (como propia, superuser) ----'
INSERT INTO tenants (id, nombre, estado, estado_custodia, created_at)
  VALUES ('11111111-1111-1111-1111-111111111111', 'Test T1', 1, 1, now()),
         ('22222222-2222-2222-2222-222222222222', 'Test T2', 1, 1, now())
  ON CONFLICT DO NOTHING;

INSERT INTO personas (id, tipo_documento, documento, nombres, apellidos, created_at)
  VALUES ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 1, '99991', 'Ana', 'Test', now()),
         ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 1, '99992', 'Beto', 'Test', now())
  ON CONFLICT DO NOTHING;

INSERT INTO usuarios_tenant (id, tenant_id, persona_id, rol, estado, created_at)
  VALUES ('cccccccc-cccc-cccc-cccc-cccccccccccc', '11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Residente', 1, now()),
         ('dddddddd-dddd-dddd-dddd-dddddddddddd', '22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'Residente', 1, now())
  ON CONFLICT DO NOTHING;

\echo '---- Probar RLS como propia_app (rol no privilegiado) ----'
SET ROLE propia_app;

SELECT set_config('app.tenant_id', '11111111-1111-1111-1111-111111111111', false);
SELECT 'Como T1' AS contexto, count(*) AS filas FROM usuarios_tenant;

SELECT set_config('app.tenant_id', '22222222-2222-2222-2222-222222222222', false);
SELECT 'Como T2' AS contexto, count(*) AS filas FROM usuarios_tenant;

SELECT set_config('app.tenant_id', '', false);
SELECT 'Sin tenant' AS contexto, count(*) AS filas FROM usuarios_tenant;

RESET ROLE;

\echo '---- Cleanup ----'
DELETE FROM usuarios_tenant WHERE id IN ('cccccccc-cccc-cccc-cccc-cccccccccccc', 'dddddddd-dddd-dddd-dddd-dddddddddddd');
DELETE FROM personas WHERE id IN ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');
DELETE FROM tenants WHERE id IN ('11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222');

\echo '---- Test trigger inmutable (DELETE debe fallar) ----'
DO $$
BEGIN
  INSERT INTO super_admin_logs (id, actor_id, actor_email, accion, created_at)
    VALUES (gen_random_uuid(), gen_random_uuid(), 'test@propia.com.co', 'TEST', now());
  BEGIN
    DELETE FROM super_admin_logs WHERE accion = 'TEST';
    RAISE EXCEPTION 'ERROR: el trigger no bloqueo el DELETE';
  EXCEPTION WHEN raise_exception THEN
    RAISE NOTICE 'OK: trigger bloqueo DELETE -> %', SQLERRM;
  END;
  TRUNCATE super_admin_logs;
END $$;
