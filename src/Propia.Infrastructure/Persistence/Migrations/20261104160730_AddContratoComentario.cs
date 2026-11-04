using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContratoComentario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contrato_comentarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contrato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    autor_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    autor_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contrato_comentarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_contrato_comentarios_contratos_servicio_contrato_id",
                        column: x => x.contrato_id,
                        principalTable: "contratos_servicio",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contrato_comentarios_contrato_id",
                table: "contrato_comentarios",
                column: "contrato_id");

            migrationBuilder.CreateIndex(
                name: "IX_contrato_comentarios_tenant_id_contrato_id",
                table: "contrato_comentarios",
                columns: new[] { "tenant_id", "contrato_id" });

            // RLS: aislar los comentarios por tenant (FORCE + policy + GRANT), como el resto de tablas de
            // tenant. RlsCoverageTests exige RLS en toda tabla con tenant_id.
            migrationBuilder.Sql(@"
                ALTER TABLE contrato_comentarios ENABLE ROW LEVEL SECURITY;
                ALTER TABLE contrato_comentarios FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON contrato_comentarios
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON contrato_comentarios TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contrato_comentarios");
        }
    }
}
