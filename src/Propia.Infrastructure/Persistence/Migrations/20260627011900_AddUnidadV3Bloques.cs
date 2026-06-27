using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnidadV3Bloques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cuota_mensual",
                table: "unidades_privadas",
                type: "numeric(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "unidad_arriendos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concepto = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    valor_mensual = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    referencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_arriendos", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidad_arriendos_unidades_privadas_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unidad_empleadas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    documento = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    celular = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    horario = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_empleadas", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidad_empleadas_unidades_privadas_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unidad_mascotas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    raza = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_mascotas", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidad_mascotas_unidades_privadas_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unidad_placas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    placa = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    tipo_vehiculo = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidad_placas", x => x.id);
                    table.ForeignKey(
                        name: "FK_unidad_placas_unidades_privadas_unidad_id",
                        column: x => x.unidad_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_unidad_arriendos_tenant_id",
                table: "unidad_arriendos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_arriendos_unidad_id",
                table: "unidad_arriendos",
                column: "unidad_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_empleadas_tenant_id",
                table: "unidad_empleadas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_empleadas_unidad_id",
                table: "unidad_empleadas",
                column: "unidad_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_mascotas_tenant_id",
                table: "unidad_mascotas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_mascotas_unidad_id",
                table: "unidad_mascotas",
                column: "unidad_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_placas_tenant_id",
                table: "unidad_placas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_placas_unidad_id",
                table: "unidad_placas",
                column: "unidad_id");

            // RLS por tenant (PostgreSQL) - mismo patron que unidad_documentos / unidad_personas.
            foreach (var t in new[] { "unidad_placas", "unidad_arriendos", "unidad_mascotas", "unidad_empleadas" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {t} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {t} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {t}
                        USING (tenant_id = current_tenant_id())
                        WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {t} TO propia_app;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unidad_arriendos");

            migrationBuilder.DropTable(
                name: "unidad_empleadas");

            migrationBuilder.DropTable(
                name: "unidad_mascotas");

            migrationBuilder.DropTable(
                name: "unidad_placas");

            migrationBuilder.DropColumn(
                name: "cuota_mensual",
                table: "unidades_privadas");
        }
    }
}
