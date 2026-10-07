using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Modulo25V2_MultiRolCargo_EstadoPendienteContacto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuario_tenant_cargos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cargo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_tenant_cargos", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_tenant_cargos_usuarios_tenant_usuario_tenant_id",
                        column: x => x.usuario_tenant_id,
                        principalTable: "usuarios_tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_tenant_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_tenant_roles", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_tenant_roles_roles_copropiedad_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles_copropiedad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_usuario_tenant_roles_usuarios_tenant_usuario_tenant_id",
                        column: x => x.usuario_tenant_id,
                        principalTable: "usuarios_tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_cargos_tenant_id",
                table: "usuario_tenant_cargos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_cargos_usuario_tenant_id_cargo",
                table: "usuario_tenant_cargos",
                columns: new[] { "usuario_tenant_id", "cargo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_roles_rol_id",
                table: "usuario_tenant_roles",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_roles_tenant_id",
                table: "usuario_tenant_roles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_roles_usuario_tenant_id_rol_id",
                table: "usuario_tenant_roles",
                columns: new[] { "usuario_tenant_id", "rol_id" },
                unique: true);

            // Backfill multi-rol: cada acceso existente con un rol principal (RolId) pasa a tener esa MISMA
            // asignacion en la tabla intermedia (no se pierde nada; UsuarioTenant.RolId se conserva como
            // rol principal/compat). La migracion corre como 'propia' (superuser) -> bypassa RLS, ve todos
            // los tenants. ON CONFLICT por si se re-ejecuta.
            migrationBuilder.Sql(@"
INSERT INTO usuario_tenant_roles (id, usuario_tenant_id, rol_id, tenant_id, created_at)
SELECT gen_random_uuid(), ut.id, ut.rol_id, ut.tenant_id, now()
FROM usuarios_tenant ut
WHERE ut.rol_id IS NOT NULL
ON CONFLICT (usuario_tenant_id, rol_id) DO NOTHING;
");

            // RLS (patron tenant_isolation con current_tenant_id(), igual que las demas tablas operativas).
            migrationBuilder.Sql(@"
ALTER TABLE usuario_tenant_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE usuario_tenant_roles FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON usuario_tenant_roles
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());

ALTER TABLE usuario_tenant_cargos ENABLE ROW LEVEL SECURITY;
ALTER TABLE usuario_tenant_cargos FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON usuario_tenant_cargos
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());
");

            // GRANT al rol de runtime (respeta RLS). Igual que el resto de tablas operativas.
            migrationBuilder.Sql(@"
GRANT SELECT, INSERT, UPDATE, DELETE ON usuario_tenant_roles TO propia_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON usuario_tenant_cargos TO propia_app;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuario_tenant_cargos");

            migrationBuilder.DropTable(
                name: "usuario_tenant_roles");
        }
    }
}
