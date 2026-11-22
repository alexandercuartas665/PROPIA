using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModuloBuzones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "pqrsd_predeterminado",
                table: "buzones_correo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "responde_pqrsd",
                table: "buzones_correo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Menu: "Buzones de correo" (cfg-buzones) en la seccion "Configuracion" del Super Admin, junto a Plantillas de
            // documentos (mismo sort_order que cfg-plantillas; el desempate por etiqueta lo deja antes). Idempotente; si no
            // existe esa seccion queda en su seccion de catalogo (Mi copropiedad).
            migrationBuilder.Sql(@"
INSERT INTO menu_overrides (id, node_key, parent_key, sort_order, created_at, is_custom, hidden)
SELECT gen_random_uuid(), 'cfg-buzones', s.node_key,
       COALESCE((SELECT h.sort_order FROM menu_overrides h WHERE h.node_key = 'cfg-plantillas' AND h.parent_key = s.node_key), 3),
       now(), false, false
FROM menu_overrides s
WHERE s.node_type = 'section' AND lower(s.label) IN ('configuración', 'configuracion')
  AND NOT EXISTS (SELECT 1 FROM menu_overrides x WHERE x.node_key = 'cfg-buzones')
ORDER BY s.sort_order NULLS LAST
LIMIT 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_overrides WHERE node_key = 'cfg-buzones';");

            migrationBuilder.DropColumn(
                name: "pqrsd_predeterminado",
                table: "buzones_correo");

            migrationBuilder.DropColumn(
                name: "responde_pqrsd",
                table: "buzones_correo");
        }
    }
}
