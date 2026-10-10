using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fase3DropTareaPrioridadEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fase 3 - Paso 0 (hallazgo 2): ANTES de dropear la columna enum, garantizar que toda tarea tenga
            // prioridad_id. La migracion corre como owner (propia, BYPASSRLS), asi que la policy no filtra filas.
            // 1) Por valor: la opcion BASE del tablero cuyo base_valor == el enum guardado.
            migrationBuilder.Sql(@"
                UPDATE tareas ta
                SET prioridad_id = tp.id
                FROM tarea_prioridades tp
                WHERE ta.prioridad_id IS NULL
                  AND ta.tablero_id = tp.tablero_id
                  AND ta.tenant_id  = tp.tenant_id
                  AND tp.es_base
                  AND tp.base_valor = ta.prioridad;
            ");
            // 2) Remanentes (enum invalido/sin match, ej. 0): caen a la opcion Normal (base_valor = 3) del tablero.
            migrationBuilder.Sql(@"
                UPDATE tareas ta
                SET prioridad_id = tp.id
                FROM tarea_prioridades tp
                WHERE ta.prioridad_id IS NULL
                  AND ta.tablero_id = tp.tablero_id
                  AND ta.tenant_id  = tp.tenant_id
                  AND tp.es_base
                  AND tp.base_valor = 3;
            ");

            migrationBuilder.DropColumn(
                name: "prioridad",
                table: "tareas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "prioridad",
                table: "tareas",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            // Reconstruye el enum (best-effort) desde la opcion: base -> su base_valor; personalizada o sin
            // opcion -> 3 (Normal). Un rollback no recupera prioridades personalizadas con fidelidad (por diseno).
            migrationBuilder.Sql(@"
                UPDATE tareas ta
                SET prioridad = COALESCE(tp.base_valor, 3)
                FROM tarea_prioridades tp
                WHERE ta.prioridad_id = tp.id;
            ");
            migrationBuilder.Sql("UPDATE tareas SET prioridad = 3 WHERE prioridad = 0;");
        }
    }
}
