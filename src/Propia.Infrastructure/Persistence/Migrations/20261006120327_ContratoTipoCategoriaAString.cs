using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ContratoTipoCategoriaAString : Migration
    {
        // tipo_contrato y categoria del contrato pasan de enum (int) a texto configurable por copropiedad.
        // Las columnas se convierten mapeando el int del enum a su etiqueta (las mismas semillas que usa
        // ContratoCamposSistema). Se hace con SQL USING CASE porque el cast directo int->text daria "1"/"2"
        // y no la etiqueta. ELSE NULL cubre valores fuera de rango (no deberia haber).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE contratos_servicio
    ALTER COLUMN tipo_contrato TYPE text
    USING (CASE tipo_contrato
        WHEN 1 THEN 'Prestacion de servicios'
        WHEN 2 THEN 'Obra'
        WHEN 3 THEN 'Mantenimiento'
        WHEN 4 THEN 'Compra y venta'
        WHEN 5 THEN 'Arrendamiento'
        WHEN 6 THEN 'Seguro'
        WHEN 7 THEN 'Licenciamiento'
        ELSE NULL END);");

            migrationBuilder.Sql(@"
ALTER TABLE contratos_servicio
    ALTER COLUMN categoria TYPE text
    USING (CASE categoria
        WHEN 1 THEN 'Administracion'
        WHEN 2 THEN 'Contabilidad'
        WHEN 3 THEN 'Asesoria'
        WHEN 4 THEN 'Aseo'
        WHEN 5 THEN 'Seguridad'
        WHEN 6 THEN 'Mantenimiento'
        WHEN 7 THEN 'Jardineria'
        WHEN 8 THEN 'Servicios publicos'
        WHEN 9 THEN 'Seguros'
        ELSE NULL END);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: texto -> int del enum. Valores propios agregados por la copropiedad (que no estan
            // en el enum) no tienen int equivalente -> NULL (se pierden al revertir, inherente a volver al enum).
            migrationBuilder.Sql(@"
ALTER TABLE contratos_servicio
    ALTER COLUMN tipo_contrato TYPE integer
    USING (CASE tipo_contrato
        WHEN 'Prestacion de servicios' THEN 1
        WHEN 'Obra' THEN 2
        WHEN 'Mantenimiento' THEN 3
        WHEN 'Compra y venta' THEN 4
        WHEN 'Arrendamiento' THEN 5
        WHEN 'Seguro' THEN 6
        WHEN 'Licenciamiento' THEN 7
        ELSE NULL END);");

            migrationBuilder.Sql(@"
ALTER TABLE contratos_servicio
    ALTER COLUMN categoria TYPE integer
    USING (CASE categoria
        WHEN 'Administracion' THEN 1
        WHEN 'Contabilidad' THEN 2
        WHEN 'Asesoria' THEN 3
        WHEN 'Aseo' THEN 4
        WHEN 'Seguridad' THEN 5
        WHEN 'Mantenimiento' THEN 6
        WHEN 'Jardineria' THEN 7
        WHEN 'Servicios publicos' THEN 8
        WHEN 'Seguros' THEN 9
        ELSE NULL END);");
        }
    }
}
