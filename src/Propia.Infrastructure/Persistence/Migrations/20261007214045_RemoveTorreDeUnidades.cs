using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTorreDeUnidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hornea la Torre en el codigo (Numero) de cada unidad ANTES de soltar torre_id/torres.
            // Si la unidad tiene torre, antepone la ultima palabra del nombre de la torre con guion
            // ("A1-101"); si no, conserva el Numero tal cual. Luego garantiza unicidad del codigo por
            // tenant: cuando el horneado produciria duplicados (mismo tenant, mismo codigo en minusculas)
            // desambigua agregando "-<n>" a partir de la 2a aparicion. Irreversible (ver Down()).
            migrationBuilder.Sql(@"
                WITH baked AS (
                  SELECT u.id, u.tenant_id,
                    CASE WHEN u.torre_id IS NOT NULL AND btrim(split_part(btrim(t.nombre),' ',-1)) <> ''
                         THEN split_part(btrim(t.nombre),' ',-1) || '-' || u.numero ELSE u.numero END AS code
                  FROM unidades_privadas u LEFT JOIN torres t ON t.id = u.torre_id),
                numbered AS (
                  SELECT id, code, row_number() OVER (PARTITION BY tenant_id, lower(code) ORDER BY id) AS rn FROM baked)
                UPDATE unidades_privadas u SET numero = CASE WHEN n.rn = 1 THEN n.code ELSE n.code || '-' || n.rn END
                FROM numbered n WHERE n.id = u.id;");

            migrationBuilder.DropForeignKey(
                name: "FK_unidades_privadas_torres_torre_id",
                table: "unidades_privadas");

            migrationBuilder.DropTable(
                name: "torres");

            migrationBuilder.DropIndex(
                name: "IX_unidades_privadas_torre_id",
                table: "unidades_privadas");

            migrationBuilder.DropColumn(
                name: "torre_id",
                table: "unidades_privadas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recrea la columna/tabla (estructura), pero el horneado del codigo en Up() NO es reversible:
            // los Numero ya incluyen el prefijo de la antigua torre y torre_id vuelve null. Es aceptable.
            migrationBuilder.AddColumn<Guid>(
                name: "torre_id",
                table: "unidades_privadas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "torres",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_pisos = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_torres", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_unidades_privadas_torre_id",
                table: "unidades_privadas",
                column: "torre_id");

            migrationBuilder.CreateIndex(
                name: "IX_torres_tenant_id",
                table: "torres",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_unidades_privadas_torres_torre_id",
                table: "unidades_privadas",
                column: "torre_id",
                principalTable: "torres",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
