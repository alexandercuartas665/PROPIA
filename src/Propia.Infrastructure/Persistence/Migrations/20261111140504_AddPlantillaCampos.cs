using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Campos propios de Configuracion > Plantillas de documentos (columna "+" de la vista tabla): catalogo
    /// por copropiedad + valor por plantilla (EAV, espejo de Zonas). Ambas tablas con RLS FORCE. Los valores
    /// se borran en cascada con la plantilla o con la definicion. Down: borra las dos tablas (y sus datos).
    /// </summary>
    public partial class AddPlantillaCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plantilla_campo_definiciones",
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
                    table.PrimaryKey("PK_plantilla_campo_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plantilla_campo_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla_documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plantilla_campo_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_plantilla_campo_valores_plantilla_campo_definiciones_defini~",
                        column: x => x.definicion_id,
                        principalTable: "plantilla_campo_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_plantilla_campo_valores_plantillas_documento_plantilla_docu~",
                        column: x => x.plantilla_documento_id,
                        principalTable: "plantillas_documento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plantilla_campo_definiciones_tenant_id",
                table: "plantilla_campo_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_plantilla_campo_definiciones_tenant_id_label",
                table: "plantilla_campo_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plantilla_campo_valores_definicion_id_plantilla_documento_id",
                table: "plantilla_campo_valores",
                columns: new[] { "definicion_id", "plantilla_documento_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plantilla_campo_valores_plantilla_documento_id",
                table: "plantilla_campo_valores",
                column: "plantilla_documento_id");

            migrationBuilder.CreateIndex(
                name: "IX_plantilla_campo_valores_tenant_id",
                table: "plantilla_campo_valores",
                column: "tenant_id");

            // RLS FORCE + policy + GRANT (regla multi-tenant; RlsCoverageTests lo exige).
            foreach (var t in new[] { "plantilla_campo_definiciones", "plantilla_campo_valores" })
            {
                migrationBuilder.Sql($"ALTER TABLE {t} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {t} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($@"
CREATE POLICY tenant_isolation ON {t}
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());");
                migrationBuilder.Sql($"GRANT SELECT, INSERT, UPDATE, DELETE ON {t} TO propia_app;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plantilla_campo_valores");

            migrationBuilder.DropTable(
                name: "plantilla_campo_definiciones");
        }
    }
}
