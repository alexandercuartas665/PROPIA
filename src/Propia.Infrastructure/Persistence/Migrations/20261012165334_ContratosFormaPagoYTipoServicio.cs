using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContratosFormaPagoYTipoServicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "forma_pago",
                table: "contratos_servicio",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_servicio_texto",
                table: "contratos_servicio",
                type: "text",
                nullable: true);

            // Backfill: cada contrato existente toma como "tipo de servicio" (texto) la etiqueta del enum
            // TipoServicio que ya tenia (columna 'tipo'). Asi la columna del catalogo arranca con paridad.
            // 'forma_pago' queda NULL (concepto nuevo: metodo de pago); el N de cuotas sigue en forma_pago_cuotas.
            migrationBuilder.Sql(@"
                UPDATE contratos_servicio SET tipo_servicio_texto = CASE tipo
                    WHEN 1 THEN 'Aseo'
                    WHEN 2 THEN 'Seguridad'
                    WHEN 3 THEN 'Mantenimiento'
                    WHEN 4 THEN 'Jardineria'
                    WHEN 5 THEN 'Mantenimiento piscina'
                    WHEN 6 THEN 'Ascensores'
                    WHEN 7 THEN 'Plagas'
                    WHEN 8 THEN 'Seguro PH'
                    WHEN 9 THEN 'Internet / WiFi'
                    WHEN 10 THEN 'Energia'
                    WHEN 11 THEN 'Agua'
                    WHEN 12 THEN 'Gas'
                    WHEN 13 THEN 'Otros'
                    ELSE NULL END
                WHERE tipo_servicio_texto IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "forma_pago",
                table: "contratos_servicio");

            migrationBuilder.DropColumn(
                name: "tipo_servicio_texto",
                table: "contratos_servicio");
        }
    }
}
