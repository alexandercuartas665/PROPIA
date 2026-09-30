using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Modulo25V2_D2_ReseedRolesBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D2 (spec 2.5 v2.0 3.2/11): re-seed a los 7 roles base + remapeo de usuarios. Mapeo
            // aprobado por Alex; validado con dry-run (ROLLBACK) + conteos. Corre en la transaccion de
            // la migracion; las aserciones DO abortan (RAISE EXCEPTION) si algun usuario/celda queda
            // huerfano tras el remap. IDs fijos del catalogo (a0*=serie base, b0*=serie extendido).
            migrationBuilder.Sql(@"
                -- 1. Administrador/Consejero: categoria Administrativo (1)
                UPDATE roles_copropiedad SET categoria = 1
                 WHERE id IN ('a0000000-0000-0000-0000-000000000001','a0000000-0000-0000-0000-000000000002');

                -- 2. Operario -> Personal Operativo (renombre EN SITIO; mismo RolId conserva sus usuarios + matriz)
                UPDATE roles_copropiedad
                   SET nombre = 'Personal Operativo',
                       descripcion = 'Personal de servicios u operativo. Solo tareas asignadas.',
                       categoria = 2
                 WHERE id = 'a0000000-0000-0000-0000-000000000005';
                UPDATE usuarios_tenant SET rol = 'Personal Operativo' WHERE rol = 'Operario';

                -- 3. Coordinador + Asistente: promover Extendido -> Base, activo, categoria Administrativo
                UPDATE roles_copropiedad SET tipo = 2, activo = true, categoria = 1
                 WHERE id IN ('b0000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000004');

                -- 4. Portal Residente (base, categoria Portal Residente=3) + matriz COPIADA de Propietario
                INSERT INTO roles_copropiedad (id, tenant_id, nombre, descripcion, tipo, es_eliminable, activo, created_at, solo_directorio, categoria)
                VALUES ('a0000000-0000-0000-0000-000000000006', NULL, 'Portal Residente',
                        'Acceso de portal para residentes (ver su unidad, PQRS, reservas, documentos). La titularidad se define por la relacion en Residentes.',
                        2, false, true, now(), false, 3);
                INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
                SELECT gen_random_uuid(), 'a0000000-0000-0000-0000-000000000006', modulo_codigo, accion, habilitado, nivel_dato, now()
                FROM rol_permisos WHERE rol_id = 'a0000000-0000-0000-0000-000000000003';

                -- 5. Terceros (base, categoria Terceros=4), matriz vacia
                INSERT INTO roles_copropiedad (id, tenant_id, nombre, descripcion, tipo, es_eliminable, activo, created_at, solo_directorio, categoria)
                VALUES ('a0000000-0000-0000-0000-000000000007', NULL, 'Terceros',
                        'Proveedores o externos con acceso acotado.', 2, false, true, now(), false, 4);

                -- 6. Trasladar facetas de siembra Propietario/Residente -> Portal Residente (merge por tenant)
                INSERT INTO roles_semilla_tenant (id, tenant_id, rol_id, facetas_semilla, solo_directorio, created_at)
                SELECT gen_random_uuid(), rs.tenant_id, 'a0000000-0000-0000-0000-000000000006',
                       string_agg(DISTINCT f, ','), bool_or(rs.solo_directorio), now()
                FROM roles_semilla_tenant rs
                CROSS JOIN LATERAL unnest(string_to_array(rs.facetas_semilla, ',')) AS f
                WHERE rs.rol_id IN ('a0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000004')
                GROUP BY rs.tenant_id;

                -- 7. Remapear usuarios + invitaciones Propietario/Residente -> Portal Residente
                UPDATE usuarios_tenant
                   SET rol_id = 'a0000000-0000-0000-0000-000000000006', rol = 'Portal Residente'
                 WHERE rol_id IN ('a0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000004');
                UPDATE usuario_invitaciones
                   SET rol_id = 'a0000000-0000-0000-0000-000000000006'
                 WHERE rol_id IN ('a0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000004');

                -- 8. Contador -> Personalizado del tenant de su usuario (preserva su matriz)
                UPDATE roles_copropiedad
                   SET tenant_id = '020200bd-855c-40ee-9121-437e4052ed1f', tipo = 4, activo = true
                 WHERE id = 'b0000000-0000-0000-0000-000000000002';

                -- 9. Borrar Propietario, Residente (cascade: rol_permisos, rol_permisos_tenant, roles_semilla_tenant viejos)
                DELETE FROM roles_copropiedad WHERE id IN ('a0000000-0000-0000-0000-000000000003','a0000000-0000-0000-0000-000000000004');

                -- 10. Borrar extendidos sin usuarios: Inmobiliaria, Revisor Fiscal, Vigilante de Seguridad
                DELETE FROM roles_copropiedad WHERE id IN ('b0000000-0000-0000-0000-000000000006','b0000000-0000-0000-0000-000000000003','b0000000-0000-0000-0000-000000000005');

                -- 11. Aserciones de integridad (abortan la migracion si fallan)
                DO $$ DECLARE h int; BEGIN
                  SELECT count(*) INTO h FROM usuarios_tenant ut WHERE ut.rol_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM roles_copropiedad r WHERE r.id = ut.rol_id);
                  IF h > 0 THEN RAISE EXCEPTION 'D2 ABORT: % usuarios_tenant con rol_id huerfano', h; END IF;
                  SELECT count(*) INTO h FROM usuario_invitaciones ui WHERE NOT EXISTS (SELECT 1 FROM roles_copropiedad r WHERE r.id = ui.rol_id);
                  IF h > 0 THEN RAISE EXCEPTION 'D2 ABORT: % usuario_invitaciones con rol_id huerfano', h; END IF;
                  SELECT count(*) INTO h FROM rol_permisos rp WHERE NOT EXISTS (SELECT 1 FROM roles_copropiedad r WHERE r.id = rp.rol_id);
                  IF h > 0 THEN RAISE EXCEPTION 'D2 ABORT: % rol_permisos huerfanos', h; END IF;
                  SELECT count(*) INTO h FROM rol_permisos_tenant rpt WHERE NOT EXISTS (SELECT 1 FROM roles_copropiedad r WHERE r.id = rpt.rol_id);
                  IF h > 0 THEN RAISE EXCEPTION 'D2 ABORT: % rol_permisos_tenant huerfanos', h; END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Migracion de DATOS no reversible automaticamente: el merge Propietario+Residente ->
            // Portal Residente pierde la distincion original y el remapeo de usuarios no se puede
            // deshacer con certeza. Para revertir se restaura desde backup. Down = no-op deliberado.
        }
    }
}
