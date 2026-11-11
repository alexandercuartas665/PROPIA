using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AsambleaCriterioMayoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Copropiedades existentes quedan en MasDe (2) = mayoria absoluta, Ley 675 (decision de Alex
            // 2026-11-10). asamblea_config ya tiene RLS; la columna nueva no cambia la policy.
            migrationBuilder.AddColumn<int>(
                name: "criterio_mayoria",
                table: "asamblea_config",
                type: "integer",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "criterio_mayoria",
                table: "asamblea_config");
        }
    }
}
