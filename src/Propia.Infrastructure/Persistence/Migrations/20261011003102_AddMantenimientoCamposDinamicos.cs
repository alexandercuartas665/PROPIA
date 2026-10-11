using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMantenimientoCamposDinamicos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "intervencion_campos_definiciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    opciones = table.Column<string>(type: "text", nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intervencion_campos_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "programacion_campos_definiciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    opciones = table.Column<string>(type: "text", nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programacion_campos_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "intervencion_campos_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mantenimiento_intervencion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intervencion_campos_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_intervencion_campos_valores_intervencion_campos_definicione~",
                        column: x => x.definicion_id,
                        principalTable: "intervencion_campos_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "programacion_campos_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    programacion_tarea_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programacion_campos_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_programacion_campos_valores_programacion_campos_definicione~",
                        column: x => x.definicion_id,
                        principalTable: "programacion_campos_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_intervencion_campos_definiciones_tenant_id",
                table: "intervencion_campos_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_intervencion_campos_definiciones_tenant_id_label",
                table: "intervencion_campos_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_intervencion_campos_valores_definicion_id_mantenimiento_int~",
                table: "intervencion_campos_valores",
                columns: new[] { "definicion_id", "mantenimiento_intervencion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_intervencion_campos_valores_tenant_id",
                table: "intervencion_campos_valores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_programacion_campos_definiciones_tenant_id",
                table: "programacion_campos_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_programacion_campos_definiciones_tenant_id_label",
                table: "programacion_campos_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_programacion_campos_valores_definicion_id_programacion_tare~",
                table: "programacion_campos_valores",
                columns: new[] { "definicion_id", "programacion_tarea_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_programacion_campos_valores_tenant_id",
                table: "programacion_campos_valores",
                column: "tenant_id");

            // RLS: aislamiento por tenant (FORCE + policy tenant_isolation + GRANT propia_app) en las 4 tablas,
            // igual que equipo_campos_* (RlsCoverageTests exige policy en toda tabla con tenant_id).
            foreach (var t in new[] { "programacion_campos_definiciones", "programacion_campos_valores",
                                      "intervencion_campos_definiciones", "intervencion_campos_valores" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {t} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {t} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {t}
                        USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {t} TO propia_app;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intervencion_campos_valores");

            migrationBuilder.DropTable(
                name: "programacion_campos_valores");

            migrationBuilder.DropTable(
                name: "intervencion_campos_definiciones");

            migrationBuilder.DropTable(
                name: "programacion_campos_definiciones");
        }
    }
}
