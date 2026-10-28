using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipoDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipo_documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipo_documentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipo_documentos_equipos_activos_equipo_activo_id",
                        column: x => x.equipo_activo_id,
                        principalTable: "equipos_activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_equipo_documentos_equipo_activo_id",
                table: "equipo_documentos",
                column: "equipo_activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipo_documentos_tenant_id",
                table: "equipo_documentos",
                column: "tenant_id");

            // RLS por tenant (mismo patron que el resto del modelo).
            migrationBuilder.Sql(@"
                ALTER TABLE equipo_documentos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE equipo_documentos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON equipo_documentos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON equipo_documentos TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipo_documentos");
        }
    }
}
