using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCronYCorreoAProgramacionTareas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cron_expresion",
                table: "programacion_tareas",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "notificar_por_correo",
                table: "programacion_tareas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "proxima_ejecucion_utc",
                table: "programacion_tareas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tipo",
                table: "programacion_tareas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "zona_horaria",
                table: "programacion_tareas",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "America/Bogota");

            migrationBuilder.CreateIndex(
                name: "IX_programacion_tareas_activa_proxima_ejecucion_utc",
                table: "programacion_tareas",
                columns: new[] { "activa", "proxima_ejecucion_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_programacion_tareas_activa_proxima_ejecucion_utc",
                table: "programacion_tareas");

            migrationBuilder.DropColumn(
                name: "cron_expresion",
                table: "programacion_tareas");

            migrationBuilder.DropColumn(
                name: "notificar_por_correo",
                table: "programacion_tareas");

            migrationBuilder.DropColumn(
                name: "proxima_ejecucion_utc",
                table: "programacion_tareas");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "programacion_tareas");

            migrationBuilder.DropColumn(
                name: "zona_horaria",
                table: "programacion_tareas");
        }
    }
}
