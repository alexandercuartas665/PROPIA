using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTareaEtiquetaTableroId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tarea_etiquetas_tenant_id_nombre",
                table: "tarea_etiquetas");

            migrationBuilder.AddColumn<Guid>(
                name: "tablero_id",
                table: "tarea_etiquetas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tarea_etiquetas_tenant_id_tablero_id_nombre",
                table: "tarea_etiquetas",
                columns: new[] { "tenant_id", "tablero_id", "nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tarea_etiquetas_tenant_id_tablero_id_nombre",
                table: "tarea_etiquetas");

            migrationBuilder.DropColumn(
                name: "tablero_id",
                table: "tarea_etiquetas");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_etiquetas_tenant_id_nombre",
                table: "tarea_etiquetas",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);
        }
    }
}
