using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Matriz de roles, Ola 2 (2026-11-21, OK de Alex): el permiso Exportar pasa a gatear el boton Descargar de
    /// las tablas (y el Excel de Reportes). Antes no lo revisaba nadie y solo el Administrador lo tenia, asi que
    /// para que nadie pierda el boton al desplegar: cada rol que tiene una celda Ver en un modulo exportable
    /// recibe Exportar en ese modulo, con el mismo valor y nivel de dato. Solo DATOS, sin cambios de esquema.
    /// - rol_permisos: catalogo global (roles base/extendidos) + roles personalizados (no tiene RLS).
    /// - rol_permisos_tenant: overrides por copropiedad (RLS FORCE): se copia el override de Ver (habilitado o
    ///   no) a Exportar, asi una copropiedad que le quito Ver a un rol tampoco le da Exportar.
    /// Idempotente: no toca celdas Exportar que ya existan.
    /// </summary>
    public partial class ExportarIgualAVer : Migration
    {
        private const string Modulos =
            "'MI_COPROPIEDAD','DIRECTORIO','USUARIOS_ACCESOS','ASAMBLEAS','PQRS','TAREAS','MANTENIMIENTO'," +
            "'RESERVAS','COMUNICACIONES','DOCUMENTOS','REPORTES','PLANTILLAS','SINIESTROS'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
SELECT gen_random_uuid(), v.rol_id, v.modulo_codigo, 6, v.habilitado, v.nivel_dato, now()
FROM rol_permisos v
WHERE v.accion = 1 AND v.modulo_codigo IN ({Modulos})
  AND NOT EXISTS (SELECT 1 FROM rol_permisos e
                  WHERE e.rol_id = v.rol_id AND e.modulo_codigo = v.modulo_codigo AND e.accion = 6);

ALTER TABLE rol_permisos_tenant NO FORCE ROW LEVEL SECURITY;
INSERT INTO rol_permisos_tenant (id, tenant_id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
SELECT gen_random_uuid(), v.tenant_id, v.rol_id, v.modulo_codigo, 6, v.habilitado, v.nivel_dato, now()
FROM rol_permisos_tenant v
WHERE v.accion = 1 AND v.modulo_codigo IN ({Modulos})
  AND NOT EXISTS (SELECT 1 FROM rol_permisos_tenant e
                  WHERE e.tenant_id = v.tenant_id AND e.rol_id = v.rol_id
                    AND e.modulo_codigo = v.modulo_codigo AND e.accion = 6);
ALTER TABLE rol_permisos_tenant FORCE ROW LEVEL SECURITY;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sin vuelta atras automatica: no se distingue la celda Exportar copiada aqui de una marcada a mano
            // despues. Revertir el codigo basta (sin el gate, Exportar vuelve a no revisarse).
        }
    }
}
