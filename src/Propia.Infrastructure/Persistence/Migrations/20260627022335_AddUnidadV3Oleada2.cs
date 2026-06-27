using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadV3Oleada2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unidad_persona_campos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    valor = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_persona_campos", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidad_persona_campos_unidad_personas_unidad_persona_id",
                        column: x => x.unidad_persona_id,
                        principalTable: "unidad_personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unidad_titularidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    rol = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    desde = table.Column<DateOnly>(type: "date", nullable: false),
                    hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_titularidades", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidad_titularidades_unidades_privadas_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_unidad_persona_campos_tenant_id",
                table: "unidad_persona_campos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_persona_campos_unidad_persona_id",
                table: "unidad_persona_campos",
                column: "unidad_persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_titularidades_tenant_id",
                table: "unidad_titularidades",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_titularidades_unidad_id",
                table: "unidad_titularidades",
                column: "unidad_id");

            // RLS por tenant (PostgreSQL) - mismo patron que el resto de tablas de unidad.
            foreach (var t in new[] { "unidad_titularidades", "unidad_persona_campos" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {t} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {t} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {t}
                        USING (tenant_id = current_tenant_id())
                        WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {t} TO propia_app;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unidad_persona_campos");

            migrationBuilder.DropTable(
                name: "unidad_titularidades");
        }
    }
}
