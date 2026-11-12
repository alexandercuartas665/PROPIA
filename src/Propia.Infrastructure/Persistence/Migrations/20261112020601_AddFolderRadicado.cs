using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderRadicado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "radicado",
                table: "folders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Backfill: radicado a los folders existentes por orden de creacion, consecutivo ANUAL por copropiedad
            // (anio de created_at en hora de Colombia). El owner pasa RLS solo sin FORCE: se quita durante el UPDATE.
            migrationBuilder.Sql(@"
ALTER TABLE folders NO FORCE ROW LEVEL SECURITY;
WITH n AS (
    SELECT id,
           EXTRACT(YEAR FROM (created_at AT TIME ZONE 'America/Bogota'))::int AS anio,
           ROW_NUMBER() OVER (PARTITION BY tenant_id, EXTRACT(YEAR FROM (created_at AT TIME ZONE 'America/Bogota'))
                              ORDER BY created_at, (parent_id IS NOT NULL), orden, nombre, id) AS num
    FROM folders
    WHERE radicado IS NULL
)
UPDATE folders f
SET radicado = 'RAD-' || n.anio || '-' || LPAD(n.num::text, 4, '0')
FROM n
WHERE f.id = n.id;
ALTER TABLE folders FORCE ROW LEVEL SECURITY;");

            migrationBuilder.CreateIndex(
                name: "IX_folders_tenant_id_radicado",
                table: "folders",
                columns: new[] { "tenant_id", "radicado" },
                unique: true,
                filter: "radicado IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_folders_tenant_id_radicado",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "radicado",
                table: "folders");
        }
    }
}
