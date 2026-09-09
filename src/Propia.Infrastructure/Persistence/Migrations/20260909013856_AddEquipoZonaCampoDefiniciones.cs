using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipoZonaCampoDefiniciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipo_campos_definiciones",
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
                    table.PrimaryKey("PK_equipo_campos_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zona_campos_definiciones",
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
                    table.PrimaryKey("PK_zona_campos_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipo_campos_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipo_campos_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_equipo_campos_valores_equipo_campos_definiciones_definicion~",
                        column: x => x.definicion_id,
                        principalTable: "equipo_campos_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zona_campos_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_comun_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_campos_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_zona_campos_valores_zona_campos_definiciones_definicion_id",
                        column: x => x.definicion_id,
                        principalTable: "zona_campos_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_equipo_campos_definiciones_tenant_id",
                table: "equipo_campos_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipo_campos_definiciones_tenant_id_label",
                table: "equipo_campos_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_equipo_campos_valores_definicion_id_equipo_activo_id",
                table: "equipo_campos_valores",
                columns: new[] { "definicion_id", "equipo_activo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_equipo_campos_valores_tenant_id",
                table: "equipo_campos_valores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_zona_campos_definiciones_tenant_id",
                table: "zona_campos_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_zona_campos_definiciones_tenant_id_label",
                table: "zona_campos_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zona_campos_valores_definicion_id_zona_comun_id",
                table: "zona_campos_valores",
                columns: new[] { "definicion_id", "zona_comun_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zona_campos_valores_tenant_id",
                table: "zona_campos_valores",
                column: "tenant_id");

            // RLS: aislamiento por tenant en las 4 tablas nuevas (FORCE RLS + policy + GRANT a propia_app),
            // igual que el resto de tablas de tenant (patron AddTenantEmailConfig).
            migrationBuilder.Sql(@"
                ALTER TABLE equipo_campos_definiciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE equipo_campos_definiciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON equipo_campos_definiciones
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON equipo_campos_definiciones TO propia_app;

                ALTER TABLE equipo_campos_valores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE equipo_campos_valores FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON equipo_campos_valores
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON equipo_campos_valores TO propia_app;

                ALTER TABLE zona_campos_definiciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE zona_campos_definiciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON zona_campos_definiciones
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON zona_campos_definiciones TO propia_app;

                ALTER TABLE zona_campos_valores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE zona_campos_valores FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON zona_campos_valores
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON zona_campos_valores TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipo_campos_valores");

            migrationBuilder.DropTable(
                name: "zona_campos_valores");

            migrationBuilder.DropTable(
                name: "equipo_campos_definiciones");

            migrationBuilder.DropTable(
                name: "zona_campos_definiciones");
        }
    }
}
