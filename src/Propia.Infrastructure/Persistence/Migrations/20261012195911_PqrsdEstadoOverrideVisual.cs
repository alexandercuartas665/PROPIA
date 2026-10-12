using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PqrsdEstadoOverrideVisual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "override_visual",
                table: "pqrsd_estados",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: marcar como OVERRIDE las columnas base cuyo nombre o color YA fue personalizado por
            // la copropiedad (difiere del de fabrica EstadosBase para su semantica_legal), para que la
            // propagacion del catalogo A&D no los pise. semantica_legal = EstadoPqrsd int: 1 Recibida,
            // 2 EnGestion, 3 Respondida, 4 Cerrada, 5 ViaInternaAgotada. Para EnGestion se aceptan ambas
            // variantes de "En gestion" (con y sin tilde) como valor de fabrica.
            migrationBuilder.Sql(@"
                UPDATE pqrsd_estados SET override_visual = true
                WHERE es_base = true AND semantica_legal IS NOT NULL AND (
                    (semantica_legal = 1 AND nombre <> 'Recibida')
                 OR (semantica_legal = 2 AND nombre NOT IN ('En gestión','En gestion'))
                 OR (semantica_legal = 3 AND nombre <> 'Respondida')
                 OR (semantica_legal = 4 AND nombre <> 'Cerrada')
                 OR (semantica_legal = 5 AND nombre <> 'Via interna agotada')
                 OR (color IS NOT NULL AND color <> CASE semantica_legal
                        WHEN 1 THEN '#94A3B8' WHEN 2 THEN '#6D4FE3' WHEN 3 THEN '#0EA5E9'
                        WHEN 4 THEN '#16A34A' WHEN 5 THEN '#DC2626' ELSE color END)
                );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "override_visual",
                table: "pqrsd_estados");
        }
    }
}
