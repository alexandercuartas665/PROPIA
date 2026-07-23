using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProgramadorHorizonteYTrazabilidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ocurrencia_utc",
                table: "tareas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "programacion_tarea_id",
                table: "tareas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "horizonte_dias",
                table: "programacion_tareas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // El job consulta las ocurrencias ya materializadas de cada regla en cada corrida
            // (cada 15 min, por tenant). Parcial: solo las tareas que vienen del programador.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS ix_tareas_programacion_ocurrencia " +
                "ON tareas (programacion_tarea_id, ocurrencia_utc) " +
                "WHERE programacion_tarea_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_tareas_programacion_ocurrencia;");

            migrationBuilder.DropColumn(
                name: "ocurrencia_utc",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "programacion_tarea_id",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "horizonte_dias",
                table: "programacion_tareas");
        }
    }
}
