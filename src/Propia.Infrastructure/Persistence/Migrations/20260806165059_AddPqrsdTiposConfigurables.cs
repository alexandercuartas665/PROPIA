using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdTiposConfigurables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tipo_id",
                table: "pqrsd_expedientes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pqrsd_tipos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dias_habiles = table.Column<int>(type: "integer", nullable: false),
                    dias_inconformidad = table.Column<int>(type: "integer", nullable: false),
                    nivel_urgencia = table.Column<int>(type: "integer", nullable: false),
                    legal = table.Column<int>(type: "integer", nullable: false),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_tipos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_expedientes_tipo_id",
                table: "pqrsd_expedientes",
                column: "tipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_tipos_tenant_id",
                table: "pqrsd_tipos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_tipos_tenant_id_nombre",
                table: "pqrsd_tipos",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_pqrsd_expedientes_pqrsd_tipos_tipo_id",
                table: "pqrsd_expedientes",
                column: "tipo_id",
                principalTable: "pqrsd_tipos",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // RLS por tenant (mismo patron que el resto del modelo).
            migrationBuilder.Sql(@"
                ALTER TABLE pqrsd_tipos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_tipos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_tipos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_tipos TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pqrsd_expedientes_pqrsd_tipos_tipo_id",
                table: "pqrsd_expedientes");

            migrationBuilder.DropTable(
                name: "pqrsd_tipos");

            migrationBuilder.DropIndex(
                name: "IX_pqrsd_expedientes_tipo_id",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "tipo_id",
                table: "pqrsd_expedientes");
        }
    }
}
