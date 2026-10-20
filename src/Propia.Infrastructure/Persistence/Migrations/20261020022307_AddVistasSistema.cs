using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVistasSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vistas_sistema",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    definicion = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vistas_sistema", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vistas_sistema_tenant_id",
                table: "vistas_sistema",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vistas_sistema_tenant_id_orden",
                table: "vistas_sistema",
                columns: new[] { "tenant_id", "orden" });

            // RLS + FORCE + policy tenant_isolation (mismo patron que el resto de Capa 2; lo exige RlsCoverageTests).
            migrationBuilder.Sql(@"
                ALTER TABLE vistas_sistema ENABLE ROW LEVEL SECURITY;
                ALTER TABLE vistas_sistema FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON vistas_sistema
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON vistas_sistema TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vistas_sistema");
        }
    }
}
