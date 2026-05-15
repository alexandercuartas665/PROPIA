using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipoOrgModulo13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "org_cargos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    es_default = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_cargos", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_cargos_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "org_cargo_permisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cargo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo = table.Column<int>(type: "integer", nullable: false),
                    nivel = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_cargo_permisos", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_cargo_permisos_org_cargos_cargo_id",
                        column: x => x.cargo_id,
                        principalTable: "org_cargos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "org_colaboradores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cargo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    fecha_vinculacion = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_desvinculacion = table.Column<DateOnly>(type: "date", nullable: true),
                    invitado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    notas_ia = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_colaboradores", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_colaboradores_org_cargos_cargo_id",
                        column: x => x.cargo_id,
                        principalTable: "org_cargos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_org_colaboradores_organizaciones_organizacion_id",
                        column: x => x.organizacion_id,
                        principalTable: "organizaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_org_colaboradores_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "org_colaborador_copropiedades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    colaborador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_capa2_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_colaborador_copropiedades", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_colaborador_copropiedades_org_colaboradores_colaborador~",
                        column: x => x.colaborador_id,
                        principalTable: "org_colaboradores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_org_colaborador_copropiedades_roles_copropiedad_rol_capa2_id",
                        column: x => x.rol_capa2_id,
                        principalTable: "roles_copropiedad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_org_colaborador_copropiedades_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "org_colaborador_historial",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    colaborador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    valor_anterior = table.Column<string>(type: "text", nullable: true),
                    valor_nuevo = table.Column<string>(type: "text", nullable: true),
                    realizado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    ocurrido_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_colaborador_historial", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_colaborador_historial_org_colaboradores_colaborador_id",
                        column: x => x.colaborador_id,
                        principalTable: "org_colaboradores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "org_colaborador_permisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    colaborador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo = table.Column<int>(type: "integer", nullable: false),
                    nivel = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_colaborador_permisos", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_colaborador_permisos_org_colaboradores_colaborador_id",
                        column: x => x.colaborador_id,
                        principalTable: "org_colaboradores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_org_cargo_permisos_cargo_id",
                table: "org_cargo_permisos",
                column: "cargo_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_cargo_permisos_cargo_id_modulo",
                table: "org_cargo_permisos",
                columns: new[] { "cargo_id", "modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_cargos_organizacion_id",
                table: "org_cargos",
                column: "organizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_cargos_organizacion_id_nombre",
                table: "org_cargos",
                columns: new[] { "organizacion_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_colaborador_copropiedades_colaborador_id",
                table: "org_colaborador_copropiedades",
                column: "colaborador_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_colaborador_copropiedades_colaborador_id_tenant_id",
                table: "org_colaborador_copropiedades",
                columns: new[] { "colaborador_id", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_colaborador_copropiedades_rol_capa2_id",
                table: "org_colaborador_copropiedades",
                column: "rol_capa2_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_colaborador_copropiedades_tenant_id",
                table: "org_colaborador_copropiedades",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_colaborador_historial_colaborador_id",
                table: "org_colaborador_historial",
                column: "colaborador_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_colaborador_historial_colaborador_id_ocurrido_at",
                table: "org_colaborador_historial",
                columns: new[] { "colaborador_id", "ocurrido_at" });

            migrationBuilder.CreateIndex(
                name: "IX_org_colaborador_permisos_colaborador_id",
                table: "org_colaborador_permisos",
                column: "colaborador_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_colaborador_permisos_colaborador_id_modulo",
                table: "org_colaborador_permisos",
                columns: new[] { "colaborador_id", "modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_colaboradores_cargo_id",
                table: "org_colaboradores",
                column: "cargo_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_colaboradores_organizacion_id",
                table: "org_colaboradores",
                column: "organizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_colaboradores_organizacion_id_estado",
                table: "org_colaboradores",
                columns: new[] { "organizacion_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_org_colaboradores_organizacion_id_persona_id",
                table: "org_colaboradores",
                columns: new[] { "organizacion_id", "persona_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_colaboradores_persona_id",
                table: "org_colaboradores",
                column: "persona_id");

            // GRANTs para propia_app (NOSUPERUSER NOBYPASSRLS) en las 6 tablas globales del modulo 1.3.
            // No llevan RLS por tenant_id (son entidades de Capa 1, filtran por organizacion_id).
            migrationBuilder.Sql(@"
                GRANT SELECT, INSERT, UPDATE, DELETE ON org_cargos TO propia_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON org_cargo_permisos TO propia_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON org_colaboradores TO propia_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON org_colaborador_permisos TO propia_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON org_colaborador_copropiedades TO propia_app;
                GRANT SELECT, INSERT ON org_colaborador_historial TO propia_app;
            ");

            // Trigger append-only en org_colaborador_historial (spec 1.3 nota dev "Trazabilidad completa").
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION org_colaborador_historial_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'org_colaborador_historial es append-only: % no permitido', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER org_col_hist_no_update
                    BEFORE UPDATE ON org_colaborador_historial
                    FOR EACH ROW EXECUTE FUNCTION org_colaborador_historial_append_only();

                CREATE TRIGGER org_col_hist_no_delete
                    BEFORE DELETE ON org_colaborador_historial
                    FOR EACH ROW EXECUTE FUNCTION org_colaborador_historial_append_only();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS org_col_hist_no_delete ON org_colaborador_historial;
                DROP TRIGGER IF EXISTS org_col_hist_no_update ON org_colaborador_historial;
                DROP FUNCTION IF EXISTS org_colaborador_historial_append_only();
            ");

            migrationBuilder.DropTable(
                name: "org_cargo_permisos");

            migrationBuilder.DropTable(
                name: "org_colaborador_copropiedades");

            migrationBuilder.DropTable(
                name: "org_colaborador_historial");

            migrationBuilder.DropTable(
                name: "org_colaborador_permisos");

            migrationBuilder.DropTable(
                name: "org_colaboradores");

            migrationBuilder.DropTable(
                name: "org_cargos");
        }
    }
}
