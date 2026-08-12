using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMotivosCierre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cerrada",
                table: "tareas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cerrada_at",
                table: "tareas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "motivo_cierre_id",
                table: "tareas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "motivo_cierre_id",
                table: "pqrsd_expedientes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "motivos_cierre",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    clasificacion = table.Column<int>(type: "integer", nullable: false),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_motivos_cierre", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_motivos_cierre_tenant_id",
                table: "motivos_cierre",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_motivos_cierre_tenant_id_modulo_nombre",
                table: "motivos_cierre",
                columns: new[] { "tenant_id", "modulo", "nombre" },
                unique: true);

            // RLS: aislar motivos_cierre por copropiedad (mismo patron que las demas tablas tenant).
            migrationBuilder.Sql(@"
                ALTER TABLE motivos_cierre ENABLE ROW LEVEL SECURITY;
                ALTER TABLE motivos_cierre FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON motivos_cierre
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON motivos_cierre TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON motivos_cierre;");
            migrationBuilder.DropTable(
                name: "motivos_cierre");

            migrationBuilder.DropColumn(
                name: "cerrada",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "cerrada_at",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "motivo_cierre_id",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "motivo_cierre_id",
                table: "pqrsd_expedientes");
        }
    }
}
