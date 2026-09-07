using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdConsecutivos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "numero_radicado",
                table: "pqrsd_respuestas",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pqrsd_consecutivo_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expediente_prefijo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expediente_incluir_anio = table.Column<bool>(type: "boolean", nullable: false),
                    expediente_padding = table.Column<int>(type: "integer", nullable: false),
                    expediente_reinicio_anual = table.Column<bool>(type: "boolean", nullable: false),
                    expediente_proximo = table.Column<int>(type: "integer", nullable: false),
                    expediente_anio = table.Column<int>(type: "integer", nullable: true),
                    respuesta_prefijo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    respuesta_incluir_anio = table.Column<bool>(type: "boolean", nullable: false),
                    respuesta_padding = table.Column<int>(type: "integer", nullable: false),
                    respuesta_reinicio_anual = table.Column<bool>(type: "boolean", nullable: false),
                    respuesta_proximo = table.Column<int>(type: "integer", nullable: false),
                    respuesta_anio = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_consecutivo_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_consecutivo_configs_tenant_id",
                table: "pqrsd_consecutivo_configs",
                column: "tenant_id");

            // RLS: aislamiento por tenant (red de seguridad final a nivel de PostgreSQL), igual que el resto
            // de tablas de Capa 2. La app corre como propia_app (no bypassa RLS).
            migrationBuilder.Sql(@"
                ALTER TABLE pqrsd_consecutivo_configs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_consecutivo_configs FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_consecutivo_configs
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_consecutivo_configs TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pqrsd_consecutivo_configs");

            migrationBuilder.DropColumn(
                name: "numero_radicado",
                table: "pqrsd_respuestas");
        }
    }
}
