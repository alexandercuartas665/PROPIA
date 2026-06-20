using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTareaCardRica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "eliminada",
                table: "tareas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "origen_referencia",
                table: "tareas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origen_tipo",
                table: "tareas",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tarea_subtareas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    hecho = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarea_subtareas", x => x.id);
                });

            // Indice + RLS por tenant (mismo patron que el resto del modelo).
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_tarea_subtareas_tarea_id ON tarea_subtareas (tarea_id);
                ALTER TABLE tarea_subtareas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tarea_subtareas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tarea_subtareas
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tarea_subtareas TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tarea_subtareas");

            migrationBuilder.DropColumn(
                name: "eliminada",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "origen_referencia",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "origen_tipo",
                table: "tareas");
        }
    }
}
