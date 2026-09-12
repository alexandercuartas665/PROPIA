using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EquipoAtlas_AddIndicesTenantIdTareas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_tarea_subtareas_tenant_id",
                table: "tarea_subtareas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_campo_valores_tenant_id",
                table: "tarea_campo_valores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_adjuntos_tenant_id",
                table: "tarea_adjuntos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tableros_tenant_id",
                table: "tableros",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tablero_usuarios_tenant_id",
                table: "tablero_usuarios",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tablero_campos_tenant_id",
                table: "tablero_campos",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tarea_subtareas_tenant_id",
                table: "tarea_subtareas");

            migrationBuilder.DropIndex(
                name: "IX_tarea_campo_valores_tenant_id",
                table: "tarea_campo_valores");

            migrationBuilder.DropIndex(
                name: "IX_tarea_adjuntos_tenant_id",
                table: "tarea_adjuntos");

            migrationBuilder.DropIndex(
                name: "IX_tableros_tenant_id",
                table: "tableros");

            migrationBuilder.DropIndex(
                name: "IX_tablero_usuarios_tenant_id",
                table: "tablero_usuarios");

            migrationBuilder.DropIndex(
                name: "IX_tablero_campos_tenant_id",
                table: "tablero_campos");
        }
    }
}
