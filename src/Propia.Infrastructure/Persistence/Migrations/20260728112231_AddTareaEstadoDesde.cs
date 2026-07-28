using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTareaEstadoDesde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "estado_desde",
                table: "tareas",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            // Backfill de filas existentes: estima el inicio del estado actual con la fecha de
            // creacion de la tarea. Mejor aproximacion disponible sin reconstruir historial.
            migrationBuilder.Sql("UPDATE tareas SET estado_desde = created_at;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "estado_desde",
                table: "tareas");
        }
    }
}
