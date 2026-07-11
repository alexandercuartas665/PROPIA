using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCopiaDeTareaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "copia_de_tarea_id",
                table: "tareas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tareas_copia_de_tarea_id",
                table: "tareas",
                column: "copia_de_tarea_id");

            migrationBuilder.CreateIndex(
                name: "IX_tareas_tenant_id_copia_de_tarea_id",
                table: "tareas",
                columns: new[] { "tenant_id", "copia_de_tarea_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_tareas_tareas_copia_de_tarea_id",
                table: "tareas",
                column: "copia_de_tarea_id",
                principalTable: "tareas",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tareas_tareas_copia_de_tarea_id",
                table: "tareas");

            migrationBuilder.DropIndex(
                name: "IX_tareas_copia_de_tarea_id",
                table: "tareas");

            migrationBuilder.DropIndex(
                name: "IX_tareas_tenant_id_copia_de_tarea_id",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "copia_de_tarea_id",
                table: "tareas");
        }
    }
}
