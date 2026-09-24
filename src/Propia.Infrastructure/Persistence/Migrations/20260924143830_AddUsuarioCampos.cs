using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioCampos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuario_campos_definiciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    opciones = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_campos_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario_campos_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_campos_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_campos_valores_usuario_campos_definiciones_definici~",
                        column: x => x.definicion_id,
                        principalTable: "usuario_campos_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_usuario_campos_definiciones_tenant_id",
                table: "usuario_campos_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_campos_definiciones_tenant_id_label",
                table: "usuario_campos_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_campos_valores_definicion_id_usuario_tenant_id",
                table: "usuario_campos_valores",
                columns: new[] { "definicion_id", "usuario_tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_campos_valores_tenant_id",
                table: "usuario_campos_valores",
                column: "tenant_id");

            // RLS: aislamiento por tenant en las 2 tablas nuevas (FORCE RLS + policy + GRANT a propia_app),
            // igual que el resto de tablas de tenant (patron AddEquipoZonaCampoDefiniciones).
            migrationBuilder.Sql(@"
                ALTER TABLE usuario_campos_definiciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE usuario_campos_definiciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON usuario_campos_definiciones
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON usuario_campos_definiciones TO propia_app;

                ALTER TABLE usuario_campos_valores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE usuario_campos_valores FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON usuario_campos_valores
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON usuario_campos_valores TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuario_campos_valores");

            migrationBuilder.DropTable(
                name: "usuario_campos_definiciones");
        }
    }
}
