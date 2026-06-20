using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTablerosTrabajo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "tareas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "es_proyecto",
                table: "tareas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "hora_fin",
                table: "tareas",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "hora_inicio",
                table: "tareas",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "progreso",
                table: "tareas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "tablero_id",
                table: "tareas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor",
                table: "tareas",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tablero_id",
                table: "tarea_estados",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tableros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    color = table.Column<string>(type: "text", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tableros", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tarea_adjuntos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarea_adjuntos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tarea_campo_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tablero_campo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarea_campo_valores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tablero_campos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tablero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tablero_campos", x => x.id);
                    table.ForeignKey(
                        name: "FK_tablero_campos_tableros_tablero_id",
                        column: x => x.tablero_id,
                        principalTable: "tableros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tablero_usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tablero_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tablero_usuarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_tablero_usuarios_tableros_tablero_id",
                        column: x => x.tablero_id,
                        principalTable: "tableros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tablero_campos_tablero_id",
                table: "tablero_campos",
                column: "tablero_id");

            migrationBuilder.CreateIndex(
                name: "IX_tablero_usuarios_tablero_id",
                table: "tablero_usuarios",
                column: "tablero_id");

            // RLS por tenant para las tablas nuevas (mismo patron que el resto del modelo).
            foreach (var tabla in new[] { "tableros", "tablero_usuarios", "tablero_campos", "tarea_campo_valores", "tarea_adjuntos" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {tabla}
                        USING (tenant_id = current_tenant_id())
                        WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {tabla} TO propia_app;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tablero_campos");

            migrationBuilder.DropTable(
                name: "tablero_usuarios");

            migrationBuilder.DropTable(
                name: "tarea_adjuntos");

            migrationBuilder.DropTable(
                name: "tarea_campo_valores");

            migrationBuilder.DropTable(
                name: "tableros");

            migrationBuilder.DropColumn(
                name: "color",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "es_proyecto",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "hora_fin",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "hora_inicio",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "progreso",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "tablero_id",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "valor",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "tablero_id",
                table: "tarea_estados");
        }
    }
}
