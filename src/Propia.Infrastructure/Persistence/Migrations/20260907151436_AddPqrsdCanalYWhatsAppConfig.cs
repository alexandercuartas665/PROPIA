using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdCanalYWhatsAppConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "canal",
                table: "pqrsd_respuesta_destinatarios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "telefono",
                table: "pqrsd_respuesta_destinatarios",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pqrsd_whatsapp_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    plantilla_idioma = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    linea_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_whatsapp_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_whatsapp_configs_tenant_id",
                table: "pqrsd_whatsapp_configs",
                column: "tenant_id");

            // RLS: aislamiento por tenant, igual que el resto de tablas de Capa 2.
            migrationBuilder.Sql(@"
                ALTER TABLE pqrsd_whatsapp_configs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_whatsapp_configs FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_whatsapp_configs
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_whatsapp_configs TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pqrsd_whatsapp_configs");

            migrationBuilder.DropColumn(
                name: "canal",
                table: "pqrsd_respuesta_destinatarios");

            migrationBuilder.DropColumn(
                name: "telefono",
                table: "pqrsd_respuesta_destinatarios");
        }
    }
}
