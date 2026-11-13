using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiciosPublicosReferenciaPorTipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cuentas_servicio_publico_tenant_id_referencia_normalizada",
                table: "cuentas_servicio_publico");

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_servicio_publico_tenant_id_referencia_normalizada_t~",
                table: "cuentas_servicio_publico",
                columns: new[] { "tenant_id", "referencia_normalizada", "tipo" },
                unique: true,
                filter: "referencia_normalizada IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cuentas_servicio_publico_tenant_id_referencia_normalizada_t~",
                table: "cuentas_servicio_publico");

            migrationBuilder.CreateIndex(
                name: "IX_cuentas_servicio_publico_tenant_id_referencia_normalizada",
                table: "cuentas_servicio_publico",
                columns: new[] { "tenant_id", "referencia_normalizada" },
                unique: true,
                filter: "referencia_normalizada IS NOT NULL");
        }
    }
}
