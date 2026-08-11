using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdAsignadoProgresoComentarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "asignado_persona_id",
                table: "pqrsd_expedientes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "progreso",
                table: "pqrsd_expedientes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "pqrsd_comentarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pqrsd_expediente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_pqrsd_comentarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_pqrsd_comentarios_pqrsd_expedientes_pqrsd_expediente_id",
                        column: x => x.pqrsd_expediente_id,
                        principalTable: "pqrsd_expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_comentarios_pqrsd_expediente_id",
                table: "pqrsd_comentarios",
                column: "pqrsd_expediente_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_comentarios_tenant_id",
                table: "pqrsd_comentarios",
                column: "tenant_id");

            migrationBuilder.Sql(@"
                ALTER TABLE pqrsd_comentarios ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_comentarios FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_comentarios
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_comentarios TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pqrsd_comentarios");

            migrationBuilder.DropColumn(
                name: "asignado_persona_id",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "progreso",
                table: "pqrsd_expedientes");
        }
    }
}
