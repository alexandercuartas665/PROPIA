using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTiposCoeficiente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tipos_coeficiente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    es_principal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tipos_coeficiente", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidad_coeficientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_coeficiente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_coeficientes", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidad_coeficientes_tipos_coeficiente_tipo_coeficiente_id",
                        column: x => x.tipo_coeficiente_id,
                        principalTable: "tipos_coeficiente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_unidad_coeficientes_unidades_privadas_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tipos_coeficiente_tenant_id",
                table: "tipos_coeficiente",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tipos_coeficiente_tenant_id_nombre",
                table: "tipos_coeficiente",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidad_coeficientes_tenant_id",
                table: "unidad_coeficientes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_coeficientes_tipo_coeficiente_id",
                table: "unidad_coeficientes",
                column: "tipo_coeficiente_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_coeficientes_unidad_id_tipo_coeficiente_id",
                table: "unidad_coeficientes",
                columns: new[] { "unidad_id", "tipo_coeficiente_id" },
                unique: true);

            // RLS para ambas tablas (mismo patron que el resto de Capa 2)
            migrationBuilder.Sql(@"
                ALTER TABLE tipos_coeficiente ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tipos_coeficiente FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tipos_coeficiente
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tipos_coeficiente TO propia_app;

                ALTER TABLE unidad_coeficientes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE unidad_coeficientes FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON unidad_coeficientes
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON unidad_coeficientes TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON unidad_coeficientes;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON tipos_coeficiente;");
            migrationBuilder.DropTable(
                name: "unidad_coeficientes");

            migrationBuilder.DropTable(
                name: "tipos_coeficiente");
        }
    }
}
