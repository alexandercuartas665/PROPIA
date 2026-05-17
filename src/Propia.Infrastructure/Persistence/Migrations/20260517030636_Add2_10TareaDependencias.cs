using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add2_10TareaDependencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tarea_dependencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depende_de_tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tarea_dependencias", x => x.id);
                    table.ForeignKey(
                        name: "FK_tarea_dependencias_tareas_depende_de_tarea_id",
                        column: x => x.depende_de_tarea_id,
                        principalTable: "tareas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tarea_dependencias_tareas_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tarea_dependencias_depende_de_tarea_id",
                table: "tarea_dependencias",
                column: "depende_de_tarea_id");

            migrationBuilder.CreateIndex(
                name: "IX_tarea_dependencias_tarea_id_depende_de_tarea_id",
                table: "tarea_dependencias",
                columns: new[] { "tarea_id", "depende_de_tarea_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tarea_dependencias_tenant_id",
                table: "tarea_dependencias",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tarea_dependencias");
        }
    }
}
