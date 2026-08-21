using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInformesGestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "informe_plantillas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_informe_plantillas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "informes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uuid", nullable: true),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    periodo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    generado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_informes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "informe_plantilla_secciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    prompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_informe_plantilla_secciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_informe_plantilla_secciones_informe_plantillas_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "informe_plantillas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "informe_secciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    informe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    prompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    contenido = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_informe_secciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_informe_secciones_informes_informe_id",
                        column: x => x.informe_id,
                        principalTable: "informes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_informe_plantilla_secciones_plantilla_id",
                table: "informe_plantilla_secciones",
                column: "plantilla_id");

            migrationBuilder.CreateIndex(
                name: "IX_informe_plantilla_secciones_tenant_id_plantilla_id",
                table: "informe_plantilla_secciones",
                columns: new[] { "tenant_id", "plantilla_id" });

            migrationBuilder.CreateIndex(
                name: "IX_informe_plantillas_tenant_id",
                table: "informe_plantillas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_informe_secciones_informe_id",
                table: "informe_secciones",
                column: "informe_id");

            migrationBuilder.CreateIndex(
                name: "IX_informe_secciones_tenant_id_informe_id",
                table: "informe_secciones",
                columns: new[] { "tenant_id", "informe_id" });

            migrationBuilder.CreateIndex(
                name: "IX_informes_tenant_id",
                table: "informes",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "informe_plantilla_secciones");

            migrationBuilder.DropTable(
                name: "informe_secciones");

            migrationBuilder.DropTable(
                name: "informe_plantillas");

            migrationBuilder.DropTable(
                name: "informes");
        }
    }
}
