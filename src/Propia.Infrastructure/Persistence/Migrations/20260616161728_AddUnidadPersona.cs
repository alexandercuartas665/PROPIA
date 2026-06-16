using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadPersona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unidad_personas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol = table.Column<int>(type: "integer", nullable: false),
                    habita = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_personas", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidad_personas_unidades_privadas_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_unidad_personas_tenant_id",
                table: "unidad_personas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_personas_tenant_id_unidad_id_persona_id_rol",
                table: "unidad_personas",
                columns: new[] { "tenant_id", "unidad_id", "persona_id", "rol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidad_personas_unidad_id",
                table: "unidad_personas",
                column: "unidad_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unidad_personas");
        }
    }
}
