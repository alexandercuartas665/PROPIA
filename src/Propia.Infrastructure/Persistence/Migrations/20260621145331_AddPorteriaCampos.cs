using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPorteriaCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "campos_valores_json",
                table: "registros_visita",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "porteria_campos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    opciones = table.Column<string>(type: "text", nullable: true),
                    mostrar_en_filtro = table.Column<bool>(type: "boolean", nullable: false),
                    columna = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_porteria_campos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_porteria_campos_tenant_id",
                table: "porteria_campos",
                column: "tenant_id");

            // RLS por tenant (mismo patron que el resto del modelo).
            migrationBuilder.Sql(@"
                ALTER TABLE porteria_campos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE porteria_campos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON porteria_campos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON porteria_campos TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "porteria_campos");

            migrationBuilder.DropColumn(
                name: "campos_valores_json",
                table: "registros_visita");
        }
    }
}
