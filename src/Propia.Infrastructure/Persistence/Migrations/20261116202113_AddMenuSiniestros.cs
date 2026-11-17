using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuSiniestros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Menu: "Siniestros" (fin-siniestros) va junto a Seguros. En el catalogo base ya cuelga de Finanzas
            // (orden 6); si el Super Admin movio Seguros a otra seccion, se ubica en esa misma seccion con el
            // mismo sort_order (el desempate por etiqueta deja "Seguros" antes de "Siniestros"). Si Seguros no
            // fue movido, no hace falta override. Idempotente.
            migrationBuilder.Sql(@"
INSERT INTO menu_overrides (id, node_key, parent_key, sort_order, created_at, is_custom, hidden)
SELECT gen_random_uuid(), 'fin-siniestros', s.parent_key, s.sort_order, now(), false, false
FROM menu_overrides s
WHERE s.node_key = 'fin-seguros' AND s.parent_key IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM menu_overrides x WHERE x.node_key = 'fin-siniestros');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_overrides WHERE node_key = 'fin-siniestros';");
        }
    }
}
