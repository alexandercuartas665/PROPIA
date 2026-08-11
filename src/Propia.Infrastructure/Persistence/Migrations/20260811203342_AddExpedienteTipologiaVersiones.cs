using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpedienteTipologiaVersiones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "numero_versiones",
                table: "expediente_tipologias",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "expediente_tipologia_versiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expediente_tipologia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    archivo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    archivo_nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    archivo_mime = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    archivo_tamano = table.Column<long>(type: "bigint", nullable: false),
                    notas_cambio = table.Column<string>(type: "text", nullable: true),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expediente_tipologia_versiones", x => x.id);
                    table.ForeignKey(
                        name: "FK_expediente_tipologia_versiones_expediente_tipologias_expedi~",
                        column: x => x.expediente_tipologia_id,
                        principalTable: "expediente_tipologias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expediente_tipologia_versiones_expediente_tipologia_id_nume~",
                table: "expediente_tipologia_versiones",
                columns: new[] { "expediente_tipologia_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expediente_tipologia_versiones_tenant_id",
                table: "expediente_tipologia_versiones",
                column: "tenant_id");

            // RLS (aislamiento por tenant) igual que el resto de tablas tenant-scoped.
            migrationBuilder.Sql(@"
                ALTER TABLE expediente_tipologia_versiones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE expediente_tipologia_versiones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON expediente_tipologia_versiones
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON expediente_tipologia_versiones TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON expediente_tipologia_versiones;");

            migrationBuilder.DropTable(
                name: "expediente_tipologia_versiones");

            migrationBuilder.DropColumn(
                name: "numero_versiones",
                table: "expediente_tipologias");
        }
    }
}
