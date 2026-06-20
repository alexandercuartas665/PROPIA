using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TareaEstadoUniquePorTablero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tarea_estados_tenant_id_nombre",
                table: "tarea_estados");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_estados_tenant_id_tablero_id_nombre",
                table: "tarea_estados",
                columns: new[] { "tenant_id", "tablero_id", "nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tarea_estados_tenant_id_tablero_id_nombre",
                table: "tarea_estados");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_estados_tenant_id_nombre",
                table: "tarea_estados",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);
        }
    }
}
