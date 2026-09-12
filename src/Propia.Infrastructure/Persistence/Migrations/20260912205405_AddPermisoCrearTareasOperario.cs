using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPermisoCrearTareasOperario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Solicitud SOLICITUD_ATLAS_01. La matriz de roles aprobada da al Operario Ver/Crear/Editar
            // sobre TAREAS, pero en la base solo existian Ver (1) y Editar (3). Como la convencion del
            // proyecto es POST -> Crear, sin esta fila un Operario no puede crear una tarea, ni
            // comentarla, ni subir un adjunto, ni etiquetarla, ni agregar un colaborador, ni duplicarla.
            //
            // roles_copropiedad y rol_permisos son GLOBALES (tenant_id NULL): la fila aplica a todas las
            // copropiedades, por eso la migracion es de la sesion dev principal y no de un agente.
            //
            // nivel_dato = 1 es el MISMO que ya tienen (Operario, TAREAS, 1) y (Operario, TAREAS, 3),
            // comprobado en propia_dev antes de escribirlo; no se inventa un valor nuevo.
            //
            // NO se concede Aprobar (5) a proposito: en el modulo 2.10 la configuracion del tablero
            // (estados, etiquetas, tableros, campos e invitar gente) exige Aprobar, para que trabajar en
            // el tablero y configurarlo no sean el mismo permiso.
            //
            // Idempotente: si la fila ya existe no hace nada (ademas hay unico en rol_id+modulo+accion).
            migrationBuilder.Sql(@"
                INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
                SELECT gen_random_uuid(), r.id, 'TAREAS', 2, true, 1, now()
                FROM roles_copropiedad r
                WHERE r.nombre = 'Operario'
                  AND NOT EXISTS (
                      SELECT 1 FROM rol_permisos p
                      WHERE p.rol_id = r.id AND p.modulo_codigo = 'TAREAS' AND p.accion = 2);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM rol_permisos p USING roles_copropiedad r
                WHERE p.rol_id = r.id AND r.nombre = 'Operario'
                  AND p.modulo_codigo = 'TAREAS' AND p.accion = 2;
            ");
        }
    }
}
