using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdRespuestaVersiones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pqrsd_respuesta_versiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    respuesta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    cuerpo_html = table.Column<string>(type: "text", nullable: false),
                    asunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    autor_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_respuesta_versiones", x => x.id);
                    table.ForeignKey(
                        name: "FK_pqrsd_respuesta_versiones_pqrsd_respuestas_respuesta_id",
                        column: x => x.respuesta_id,
                        principalTable: "pqrsd_respuestas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_respuesta_versiones_respuesta_id_numero",
                table: "pqrsd_respuesta_versiones",
                columns: new[] { "respuesta_id", "numero" });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_respuesta_versiones_tenant_id",
                table: "pqrsd_respuesta_versiones",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pqrsd_respuesta_versiones");
        }
    }
}
