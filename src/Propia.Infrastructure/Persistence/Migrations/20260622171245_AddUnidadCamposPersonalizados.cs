using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadCamposPersonalizados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unidad_campos_definiciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_campos_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidad_campos_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_campos_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidad_campos_valores_unidad_campos_definiciones_definicion~",
                        column: x => x.definicion_id,
                        principalTable: "unidad_campos_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_unidad_campos_valores_unidades_privadas_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_unidad_campos_definiciones_tenant_id",
                table: "unidad_campos_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_campos_definiciones_tenant_id_label",
                table: "unidad_campos_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidad_campos_valores_definicion_id_unidad_id",
                table: "unidad_campos_valores",
                columns: new[] { "definicion_id", "unidad_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidad_campos_valores_tenant_id",
                table: "unidad_campos_valores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_campos_valores_unidad_id",
                table: "unidad_campos_valores",
                column: "unidad_id");

            // RLS por tenant para las tablas nuevas (mismo patron que el resto del modelo).
            foreach (var tabla in new[] { "unidad_campos_definiciones", "unidad_campos_valores" })
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
                name: "unidad_campos_valores");

            migrationBuilder.DropTable(
                name: "unidad_campos_definiciones");
        }
    }
}
