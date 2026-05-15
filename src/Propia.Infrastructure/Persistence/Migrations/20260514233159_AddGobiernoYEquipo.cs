using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGobiernoYEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_conformacion = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "miembros_equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol = table.Column<int>(type: "integer", nullable: false),
                    rol_personalizado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    fecha_vinculacion = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    es_usuario_sistema = table.Column<bool>(type: "boolean", nullable: false),
                    telefono = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_miembros_equipo", x => x.id);
                    table.ForeignKey(
                        name: "FK_miembros_equipo_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "revisores_fiscales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_tarjeta_profesional = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fecha_posesion = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revisores_fiscales", x => x.id);
                    table.ForeignKey(
                        name: "FK_revisores_fiscales_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comite_miembros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comite_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cargo_en_comite = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comite_miembros", x => x.id);
                    table.ForeignKey(
                        name: "FK_comite_miembros_comites_comite_id",
                        column: x => x.comite_id,
                        principalTable: "comites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comite_miembros_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comite_miembros_comite_id_persona_id",
                table: "comite_miembros",
                columns: new[] { "comite_id", "persona_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comite_miembros_persona_id",
                table: "comite_miembros",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_comite_miembros_tenant_id",
                table: "comite_miembros",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_comites_tenant_id",
                table: "comites",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_comites_tenant_id_nombre",
                table: "comites",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_miembros_equipo_persona_id",
                table: "miembros_equipo",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_miembros_equipo_tenant_id",
                table: "miembros_equipo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_miembros_equipo_tenant_id_persona_id_rol",
                table: "miembros_equipo",
                columns: new[] { "tenant_id", "persona_id", "rol" });

            migrationBuilder.CreateIndex(
                name: "IX_revisores_fiscales_persona_id",
                table: "revisores_fiscales",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_revisores_fiscales_tenant_id",
                table: "revisores_fiscales",
                column: "tenant_id");

            // RLS para las 4 tablas (Comite, ComiteMiembro, RevisorFiscal, MiembroEquipo)
            migrationBuilder.Sql(@"
                ALTER TABLE comites ENABLE ROW LEVEL SECURITY;
                ALTER TABLE comites FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON comites
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON comites TO propia_app;

                ALTER TABLE comite_miembros ENABLE ROW LEVEL SECURITY;
                ALTER TABLE comite_miembros FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON comite_miembros
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON comite_miembros TO propia_app;

                ALTER TABLE revisores_fiscales ENABLE ROW LEVEL SECURITY;
                ALTER TABLE revisores_fiscales FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON revisores_fiscales
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON revisores_fiscales TO propia_app;

                ALTER TABLE miembros_equipo ENABLE ROW LEVEL SECURITY;
                ALTER TABLE miembros_equipo FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON miembros_equipo
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON miembros_equipo TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation ON miembros_equipo;
                DROP POLICY IF EXISTS tenant_isolation ON revisores_fiscales;
                DROP POLICY IF EXISTS tenant_isolation ON comite_miembros;
                DROP POLICY IF EXISTS tenant_isolation ON comites;
            ");
            migrationBuilder.DropTable(
                name: "comite_miembros");

            migrationBuilder.DropTable(
                name: "miembros_equipo");

            migrationBuilder.DropTable(
                name: "revisores_fiscales");

            migrationBuilder.DropTable(
                name: "comites");
        }
    }
}
