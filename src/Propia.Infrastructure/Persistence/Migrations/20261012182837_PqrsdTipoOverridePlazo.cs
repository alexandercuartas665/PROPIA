using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PqrsdTipoOverridePlazo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "override_plazo",
                table: "pqrsd_tipos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: marcar como OVERRIDE los tipos base cuyo plazo YA fue personalizado por la
            // copropiedad (dias_habiles != plazo de fabrica PqrsdCatalogo.PlazosBase para su 'legal'),
            // para que la propagacion del catalogo A&D no pise esa personalizacion. 'legal' = TipoPqrsd int:
            // 1 Peticion=15, 2 SolicitudDocumentos=10, 3 Consulta=30, 4 Queja=15, 5 Reclamo=15,
            // 6 Sugerencia=15, 7 Denuncia=15, 8 Felicitacion=5.
            migrationBuilder.Sql(@"
                UPDATE pqrsd_tipos SET override_plazo = true
                WHERE es_base = true AND dias_habiles <> CASE legal
                    WHEN 1 THEN 15 WHEN 2 THEN 10 WHEN 3 THEN 30 WHEN 4 THEN 15
                    WHEN 5 THEN 15 WHEN 6 THEN 15 WHEN 7 THEN 15 WHEN 8 THEN 5
                    ELSE dias_habiles END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "override_plazo",
                table: "pqrsd_tipos");
        }
    }
}
