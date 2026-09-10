using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadCampoConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unidad_campos_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campo_clave = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    alias = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    opciones = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_campos_config", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_unidad_campos_config_tenant_id",
                table: "unidad_campos_config",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_campos_config_tenant_id_campo_clave",
                table: "unidad_campos_config",
                columns: new[] { "tenant_id", "campo_clave" },
                unique: true);

            // RLS: aislamiento por tenant (misma red de seguridad que el resto de tablas de Capa 2).
            migrationBuilder.Sql(@"
                ALTER TABLE unidad_campos_config ENABLE ROW LEVEL SECURITY;
                ALTER TABLE unidad_campos_config FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON unidad_campos_config
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON unidad_campos_config TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON unidad_campos_config;");
            migrationBuilder.DropTable(
                name: "unidad_campos_config");
        }
    }
}
