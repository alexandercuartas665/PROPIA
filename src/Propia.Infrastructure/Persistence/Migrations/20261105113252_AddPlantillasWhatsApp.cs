using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlantillasWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plantillas_whatsapp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    language = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    categoria = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    encabezado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    encabezado_texto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    encabezado_media_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    cuerpo = table.Column<string>(type: "text", nullable: false),
                    pie_texto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    variables_json = table.Column<string>(type: "jsonb", nullable: false),
                    botones_json = table.Column<string>(type: "jsonb", nullable: true),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    whats_app_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waba_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider_template_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    motivo_rechazo = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plantillas_whatsapp", x => x.id);
                    table.ForeignKey(
                        name: "FK_plantillas_whatsapp_whats_app_lines_whats_app_line_id",
                        column: x => x.whats_app_line_id,
                        principalTable: "whats_app_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_whatsapp_tenant_id_activa",
                table: "plantillas_whatsapp",
                columns: new[] { "tenant_id", "activa" });

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_whatsapp_tenant_id_name_language",
                table: "plantillas_whatsapp",
                columns: new[] { "tenant_id", "name", "language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plantillas_whatsapp_whats_app_line_id",
                table: "plantillas_whatsapp",
                column: "whats_app_line_id");

            // RLS: aislar las plantillas por tenant (FORCE + policy + GRANT), como el resto de tablas de
            // tenant. RlsCoverageTests exige RLS en toda tabla con tenant_id.
            migrationBuilder.Sql(@"
                ALTER TABLE plantillas_whatsapp ENABLE ROW LEVEL SECURITY;
                ALTER TABLE plantillas_whatsapp FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON plantillas_whatsapp
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON plantillas_whatsapp TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plantillas_whatsapp");
        }
    }
}
