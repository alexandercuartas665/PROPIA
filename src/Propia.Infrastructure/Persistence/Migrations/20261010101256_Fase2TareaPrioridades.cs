using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fase2TareaPrioridades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "prioridad_id",
                table: "tareas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tarea_prioridades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tablero_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    base_valor = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarea_prioridades", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tareas_prioridad_id",
                table: "tareas",
                column: "prioridad_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_prioridades_tenant_id",
                table: "tarea_prioridades",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_prioridades_tenant_id_tablero_id_nombre",
                table: "tarea_prioridades",
                columns: new[] { "tenant_id", "tablero_id", "nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tareas_tarea_prioridades_prioridad_id",
                table: "tareas",
                column: "prioridad_id",
                principalTable: "tarea_prioridades",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // RLS: aislamiento por tenant (FORCE RLS + policy + GRANT a propia_app), igual que tarea_estados.
            migrationBuilder.Sql(@"
                ALTER TABLE tarea_prioridades ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tarea_prioridades FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tarea_prioridades
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tarea_prioridades TO propia_app;
            ");

            // Fase 2: sembrar las 4 prioridades de fabrica en CADA tablero existente (una fila por tablero).
            // Se corre como owner (propia, superuser/BYPASSRLS), asi que la policy no bloquea el INSERT.
            migrationBuilder.Sql(@"
                INSERT INTO tarea_prioridades (id, tablero_id, nombre, color, orden, es_base, base_valor, activo, created_at, tenant_id)
                SELECT gen_random_uuid(), t.id, v.nombre, v.color, v.orden, true, v.base_valor, true, now(), t.tenant_id
                FROM tableros t
                CROSS JOIN (VALUES
                    ('Urgente', '#C0383C', 1, 1),
                    ('Alta',    '#B45309', 2, 2),
                    ('Normal',  '#2563EB', 3, 3),
                    ('Baja',    '#516F90', 4, 4)
                ) AS v(nombre, color, orden, base_valor);
            ");

            // Backfill: cada tarea apunta a la prioridad base de SU tablero que corresponde a su enum actual.
            migrationBuilder.Sql(@"
                UPDATE tareas ta
                SET prioridad_id = tp.id
                FROM tarea_prioridades tp
                WHERE tp.tablero_id = ta.tablero_id
                  AND tp.tenant_id  = ta.tenant_id
                  AND tp.es_base    = true
                  AND tp.base_valor = ta.prioridad;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tareas_tarea_prioridades_prioridad_id",
                table: "tareas");

            migrationBuilder.DropTable(
                name: "tarea_prioridades");

            migrationBuilder.DropIndex(
                name: "IX_tareas_prioridad_id",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "prioridad_id",
                table: "tareas");
        }
    }
}
