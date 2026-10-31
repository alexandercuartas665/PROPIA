using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListaOpcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lista_opciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lista = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    clave = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    override_visual = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lista_opciones", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lista_opciones_tenant_id_lista",
                table: "lista_opciones",
                columns: new[] { "tenant_id", "lista" });

            migrationBuilder.CreateIndex(
                name: "IX_lista_opciones_tenant_id_lista_clave",
                table: "lista_opciones",
                columns: new[] { "tenant_id", "lista", "clave" },
                unique: true);

            // RLS por tenant (mismo patron que el resto del modelo).
            migrationBuilder.Sql(@"
                ALTER TABLE lista_opciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE lista_opciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON lista_opciones
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON lista_opciones TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lista_opciones");
        }
    }
}
