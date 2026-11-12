using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AsambleaOla4Transcripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "transcripcion_activa",
                table: "sesiones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "sesion_transcripcion_lineas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    punto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: true),
                    autor_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    texto = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    confianza = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    origen = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesion_transcripcion_lineas", x => x.id);
                    table.ForeignKey(
                        name: "FK_sesion_transcripcion_lineas_sesion_puntos_punto_id",
                        column: x => x.punto_id,
                        principalTable: "sesion_puntos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sesion_transcripcion_lineas_sesiones_sesion_id",
                        column: x => x.sesion_id,
                        principalTable: "sesiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sesion_transcripcion_lineas_punto_id",
                table: "sesion_transcripcion_lineas",
                column: "punto_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_transcripcion_lineas_sesion_id_hora",
                table: "sesion_transcripcion_lineas",
                columns: new[] { "sesion_id", "hora" });

            migrationBuilder.CreateIndex(
                name: "IX_sesion_transcripcion_lineas_tenant_id",
                table: "sesion_transcripcion_lineas",
                column: "tenant_id");

            // RLS FORCE + policy + GRANT en la tabla nueva (RlsCoverageTests lo exige).
            migrationBuilder.Sql("ALTER TABLE sesion_transcripcion_lineas ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE sesion_transcripcion_lineas FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY tenant_isolation ON sesion_transcripcion_lineas
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON sesion_transcripcion_lineas TO propia_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sesion_transcripcion_lineas");

            migrationBuilder.DropColumn(
                name: "transcripcion_activa",
                table: "sesiones");
        }
    }
}
