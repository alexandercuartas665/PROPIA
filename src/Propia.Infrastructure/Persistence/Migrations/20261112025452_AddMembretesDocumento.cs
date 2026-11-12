using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMembretesDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "encabezado_id",
                table: "plantillas_documento",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "encabezado_modo",
                table: "plantillas_documento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "pie_id",
                table: "plantillas_documento",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "pie_modo",
                table: "plantillas_documento",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "membretes_documento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    html = table.Column<string>(type: "text", nullable: false),
                    predeterminado = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membretes_documento", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_membretes_documento_predeterminado",
                table: "membretes_documento",
                columns: new[] { "tenant_id", "tipo" },
                unique: true,
                filter: "predeterminado");

            // RLS FORCE + policy + GRANT en la tabla nueva (RlsCoverageTests lo exige). Las plantillas existentes
            // quedan en Predeterminado (modo 0); cada copropiedad nace con "Institucional" al consultar sus membretes.
            migrationBuilder.Sql("ALTER TABLE membretes_documento ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE membretes_documento FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY tenant_isolation ON membretes_documento
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON membretes_documento TO propia_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "membretes_documento");

            migrationBuilder.DropColumn(
                name: "encabezado_id",
                table: "plantillas_documento");

            migrationBuilder.DropColumn(
                name: "encabezado_modo",
                table: "plantillas_documento");

            migrationBuilder.DropColumn(
                name: "pie_id",
                table: "plantillas_documento");

            migrationBuilder.DropColumn(
                name: "pie_modo",
                table: "plantillas_documento");
        }
    }
}
