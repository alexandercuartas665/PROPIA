using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Modulo25V2_RolCategoria_MatrizPorTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // categoria = CategoriaRol (Administrativo=1..Terceros=4). Default Administrativo=1 para las
            // filas existentes; la re-siembra de roles base (migracion de datos posterior) asigna la
            // categoria correcta a cada rol base.
            migrationBuilder.AddColumn<int>(
                name: "categoria",
                table: "roles_copropiedad",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "rol_permisos_tenant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    accion = table.Column<int>(type: "integer", nullable: false),
                    habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    nivel_dato = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rol_permisos_tenant", x => x.id);
                    table.ForeignKey(
                        name: "FK_rol_permisos_tenant_roles_copropiedad_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles_copropiedad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rol_permisos_tenant_rol_id",
                table: "rol_permisos_tenant",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_rol_permisos_tenant_tenant_id_rol_id_modulo_codigo_accion",
                table: "rol_permisos_tenant",
                columns: new[] { "tenant_id", "rol_id", "modulo_codigo", "accion" },
                unique: true);

            // RLS FORCE + policy de aislamiento por tenant (regla critica). rol_permisos_tenant es
            // siempre tenant-scoped (tenant_id NOT NULL) -> policy estricta como usuario_invitaciones.
            migrationBuilder.Sql(@"
                ALTER TABLE rol_permisos_tenant ENABLE ROW LEVEL SECURITY;
                ALTER TABLE rol_permisos_tenant FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON rol_permisos_tenant
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON rol_permisos_tenant TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rol_permisos_tenant");

            migrationBuilder.DropColumn(
                name: "categoria",
                table: "roles_copropiedad");
        }
    }
}
