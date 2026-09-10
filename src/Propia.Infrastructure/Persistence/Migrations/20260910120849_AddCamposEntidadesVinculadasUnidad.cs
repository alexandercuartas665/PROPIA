using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCamposEntidadesVinculadasUnidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mascota_campos_definiciones",
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
                    table.PrimaryKey("PK_mascota_campos_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "persona_campos_definiciones",
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
                    table.PrimaryKey("PK_persona_campos_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tercero_campos_definiciones",
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
                    table.PrimaryKey("PK_tercero_campos_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehiculo_campos_definiciones",
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
                    table.PrimaryKey("PK_vehiculo_campos_definiciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mascota_campos_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_mascota_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mascota_campos_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_mascota_campos_valores_mascota_campos_definiciones_definici~",
                        column: x => x.definicion_id,
                        principalTable: "mascota_campos_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "persona_campos_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persona_campos_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_persona_campos_valores_persona_campos_definiciones_definici~",
                        column: x => x.definicion_id,
                        principalTable: "persona_campos_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tercero_campos_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_empleada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tercero_campos_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_tercero_campos_valores_tercero_campos_definiciones_definici~",
                        column: x => x.definicion_id,
                        principalTable: "tercero_campos_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehiculo_campos_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_placa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehiculo_campos_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehiculo_campos_valores_vehiculo_campos_definiciones_defini~",
                        column: x => x.definicion_id,
                        principalTable: "vehiculo_campos_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mascota_campos_definiciones_tenant_id",
                table: "mascota_campos_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_mascota_campos_definiciones_tenant_id_label",
                table: "mascota_campos_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mascota_campos_valores_definicion_id_unidad_mascota_id",
                table: "mascota_campos_valores",
                columns: new[] { "definicion_id", "unidad_mascota_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mascota_campos_valores_tenant_id",
                table: "mascota_campos_valores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_persona_campos_definiciones_tenant_id",
                table: "persona_campos_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_persona_campos_definiciones_tenant_id_label",
                table: "persona_campos_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_persona_campos_valores_definicion_id_unidad_persona_id",
                table: "persona_campos_valores",
                columns: new[] { "definicion_id", "unidad_persona_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_persona_campos_valores_tenant_id",
                table: "persona_campos_valores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tercero_campos_definiciones_tenant_id",
                table: "tercero_campos_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tercero_campos_definiciones_tenant_id_label",
                table: "tercero_campos_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tercero_campos_valores_definicion_id_unidad_empleada_id",
                table: "tercero_campos_valores",
                columns: new[] { "definicion_id", "unidad_empleada_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tercero_campos_valores_tenant_id",
                table: "tercero_campos_valores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehiculo_campos_definiciones_tenant_id",
                table: "vehiculo_campos_definiciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehiculo_campos_definiciones_tenant_id_label",
                table: "vehiculo_campos_definiciones",
                columns: new[] { "tenant_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehiculo_campos_valores_definicion_id_unidad_placa_id",
                table: "vehiculo_campos_valores",
                columns: new[] { "definicion_id", "unidad_placa_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehiculo_campos_valores_tenant_id",
                table: "vehiculo_campos_valores",
                column: "tenant_id");

            // RLS: aislamiento por tenant en las 8 tablas nuevas (FORCE RLS + policy + GRANT a propia_app),
            // igual que el resto de tablas de tenant (patron AddEquipoZonaCampoDefiniciones).
            migrationBuilder.Sql(@"
                ALTER TABLE persona_campos_definiciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE persona_campos_definiciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON persona_campos_definiciones
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON persona_campos_definiciones TO propia_app;

                ALTER TABLE persona_campos_valores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE persona_campos_valores FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON persona_campos_valores
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON persona_campos_valores TO propia_app;

                ALTER TABLE vehiculo_campos_definiciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE vehiculo_campos_definiciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON vehiculo_campos_definiciones
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON vehiculo_campos_definiciones TO propia_app;

                ALTER TABLE vehiculo_campos_valores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE vehiculo_campos_valores FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON vehiculo_campos_valores
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON vehiculo_campos_valores TO propia_app;

                ALTER TABLE mascota_campos_definiciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE mascota_campos_definiciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON mascota_campos_definiciones
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON mascota_campos_definiciones TO propia_app;

                ALTER TABLE mascota_campos_valores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE mascota_campos_valores FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON mascota_campos_valores
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON mascota_campos_valores TO propia_app;

                ALTER TABLE tercero_campos_definiciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tercero_campos_definiciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tercero_campos_definiciones
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tercero_campos_definiciones TO propia_app;

                ALTER TABLE tercero_campos_valores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tercero_campos_valores FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tercero_campos_valores
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tercero_campos_valores TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mascota_campos_valores");

            migrationBuilder.DropTable(
                name: "persona_campos_valores");

            migrationBuilder.DropTable(
                name: "tercero_campos_valores");

            migrationBuilder.DropTable(
                name: "vehiculo_campos_valores");

            migrationBuilder.DropTable(
                name: "mascota_campos_definiciones");

            migrationBuilder.DropTable(
                name: "persona_campos_definiciones");

            migrationBuilder.DropTable(
                name: "tercero_campos_definiciones");

            migrationBuilder.DropTable(
                name: "vehiculo_campos_definiciones");
        }
    }
}
