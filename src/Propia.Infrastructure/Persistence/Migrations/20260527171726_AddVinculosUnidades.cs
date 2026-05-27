using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVinculosUnidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "matricula_inmobiliaria",
                table: "unidades_privadas",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "paga_administracion",
                table: "unidades_privadas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "unidad_vinculos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_principal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_asociada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    incluye_en_facturacion = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_vinculos", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidad_vinculos_unidades_privadas_unidad_asociada_id",
                        column: x => x.unidad_asociada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_unidad_vinculos_unidades_privadas_unidad_principal_id",
                        column: x => x.unidad_principal_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_unidad_vinculos_tenant_id",
                table: "unidad_vinculos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_vinculos_tenant_id_unidad_asociada_id",
                table: "unidad_vinculos",
                columns: new[] { "tenant_id", "unidad_asociada_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unidad_vinculos_unidad_asociada_id",
                table: "unidad_vinculos",
                column: "unidad_asociada_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_vinculos_unidad_principal_id",
                table: "unidad_vinculos",
                column: "unidad_principal_id");

            // RLS: aislamiento por tenant (mismo patron que las demas tablas de Capa 2).
            migrationBuilder.Sql(@"
                ALTER TABLE unidad_vinculos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE unidad_vinculos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON unidad_vinculos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON unidad_vinculos TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unidad_vinculos");

            migrationBuilder.DropColumn(
                name: "matricula_inmobiliaria",
                table: "unidades_privadas");

            migrationBuilder.DropColumn(
                name: "paga_administracion",
                table: "unidades_privadas");
        }
    }
}
