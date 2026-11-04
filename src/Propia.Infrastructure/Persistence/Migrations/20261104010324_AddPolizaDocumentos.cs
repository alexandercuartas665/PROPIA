using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPolizaDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "poliza_documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poliza_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_poliza_documentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_poliza_documentos_polizas_poliza_id",
                        column: x => x.poliza_id,
                        principalTable: "polizas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_poliza_documentos_poliza_id",
                table: "poliza_documentos",
                column: "poliza_id");

            migrationBuilder.CreateIndex(
                name: "IX_poliza_documentos_tenant_id",
                table: "poliza_documentos",
                column: "tenant_id");

            // RLS por tenant (mismo patron que el resto del modelo; RlsCoverageTests lo exige).
            migrationBuilder.Sql(@"
                ALTER TABLE poliza_documentos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE poliza_documentos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON poliza_documentos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON poliza_documentos TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "poliza_documentos");
        }
    }
}
