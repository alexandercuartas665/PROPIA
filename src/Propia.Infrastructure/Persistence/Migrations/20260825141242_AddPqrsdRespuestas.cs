using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdRespuestas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "respuesta_id",
                table: "pqrsd_adjuntos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pqrsd_respuestas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expediente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuerpo_html = table.Column<string>(type: "text", nullable: false),
                    asunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    autor_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    enviada = table.Column<bool>(type: "boolean", nullable: false),
                    enviada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_respuestas", x => x.id);
                    table.ForeignKey(
                        name: "FK_pqrsd_respuestas_pqrsd_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "pqrsd_expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_adjuntos_respuesta_id",
                table: "pqrsd_adjuntos",
                column: "respuesta_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_respuestas_expediente_id",
                table: "pqrsd_respuestas",
                column: "expediente_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_respuestas_tenant_id",
                table: "pqrsd_respuestas",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_pqrsd_adjuntos_pqrsd_respuestas_respuesta_id",
                table: "pqrsd_adjuntos",
                column: "respuesta_id",
                principalTable: "pqrsd_respuestas",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // RLS (aislamiento por tenant) para la nueva tabla, patron del proyecto.
            migrationBuilder.Sql(@"
                ALTER TABLE pqrsd_respuestas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_respuestas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_respuestas
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_respuestas TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pqrsd_adjuntos_pqrsd_respuestas_respuesta_id",
                table: "pqrsd_adjuntos");

            migrationBuilder.DropTable(
                name: "pqrsd_respuestas");

            migrationBuilder.DropIndex(
                name: "IX_pqrsd_adjuntos_respuesta_id",
                table: "pqrsd_adjuntos");

            migrationBuilder.DropColumn(
                name: "respuesta_id",
                table: "pqrsd_adjuntos");
        }
    }
}
