using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolSemillaTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roles_semilla_tenant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    facetas_semilla = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    solo_directorio = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles_semilla_tenant", x => x.id);
                    table.ForeignKey(
                        name: "FK_roles_semilla_tenant_roles_copropiedad_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles_copropiedad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_roles_semilla_tenant_rol_id",
                table: "roles_semilla_tenant",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_semilla_tenant_tenant_id_rol_id",
                table: "roles_semilla_tenant",
                columns: new[] { "tenant_id", "rol_id" },
                unique: true);

            // RLS por tenant (mismo patron que el resto del modelo).
            migrationBuilder.Sql(@"
                ALTER TABLE roles_semilla_tenant ENABLE ROW LEVEL SECURITY;
                ALTER TABLE roles_semilla_tenant FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON roles_semilla_tenant
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON roles_semilla_tenant TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "roles_semilla_tenant");
        }
    }
}
