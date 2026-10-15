using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonaComentarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "persona_comentarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persona_comentarios", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_persona_comentarios_persona_id_created_at",
                table: "persona_comentarios",
                columns: new[] { "persona_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_persona_comentarios_tenant_id",
                table: "persona_comentarios",
                column: "tenant_id");

            // RLS: aislamiento por tenant (ENABLE + FORCE + policy tenant_isolation + GRANT propia_app).
            // RlsCoverageTests exige policy en toda tabla con tenant_id.
            migrationBuilder.Sql(@"
                ALTER TABLE persona_comentarios ENABLE ROW LEVEL SECURITY;
                ALTER TABLE persona_comentarios FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON persona_comentarios
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON persona_comentarios TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "persona_comentarios");
        }
    }
}
