using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramacionTareas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "programacion_tareas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    prioridad = table.Column<int>(type: "integer", nullable: false),
                    tablero_id = table.Column<Guid>(type: "uuid", nullable: true),
                    periodicidad = table.Column<int>(type: "integer", nullable: false),
                    fecha_proxima_ejecucion = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    modulo_origen_codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    entidad_origen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origen_referencia = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tareas_generadas = table.Column<int>(type: "integer", nullable: false),
                    ultima_ejecucion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programacion_tareas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "programacion_tarea_responsables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    programacion_tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programacion_tarea_responsables", x => x.id);
                    table.ForeignKey(
                        name: "FK_programacion_tarea_responsables_programacion_tareas_program~",
                        column: x => x.programacion_tarea_id,
                        principalTable: "programacion_tareas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_programacion_tarea_responsables_programacion_tarea_id",
                table: "programacion_tarea_responsables",
                column: "programacion_tarea_id");

            migrationBuilder.CreateIndex(
                name: "IX_programacion_tarea_responsables_tenant_id",
                table: "programacion_tarea_responsables",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_programacion_tareas_activa_fecha_proxima_ejecucion",
                table: "programacion_tareas",
                columns: new[] { "activa", "fecha_proxima_ejecucion" });

            migrationBuilder.CreateIndex(
                name: "IX_programacion_tareas_tenant_id",
                table: "programacion_tareas",
                column: "tenant_id");

            // RLS por tenant (mismo patron que el resto del modelo).
            foreach (var tabla in new[] { "programacion_tareas", "programacion_tarea_responsables" })
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
                name: "programacion_tarea_responsables");

            migrationBuilder.DropTable(
                name: "programacion_tareas");
        }
    }
}
