using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoUnidadCustom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tipos_unidad_custom",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    paga_administracion_por_defecto = table.Column<bool>(type: "boolean", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_unidad_custom", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tipos_unidad_custom_tenant_id",
                table: "tipos_unidad_custom",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tipos_unidad_custom_tenant_id_nombre",
                table: "tipos_unidad_custom",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            // RLS + FORCE + policy tenant_isolation (mismo patron que el resto de Capa 2).
            migrationBuilder.Sql(@"
                ALTER TABLE tipos_unidad_custom ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tipos_unidad_custom FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tipos_unidad_custom
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tipos_unidad_custom TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON tipos_unidad_custom;");
            migrationBuilder.DropTable(
                name: "tipos_unidad_custom");
        }
    }
}
