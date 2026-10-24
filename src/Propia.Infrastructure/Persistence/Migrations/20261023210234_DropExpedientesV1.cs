using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// DESTRUCTIVA: retira las 8 tablas v1 de Expedientes/TRD (series, subseries, tipologias, expedientes y sus
    /// campos/versiones). Sus datos ya fueron COPIADOS a folders/folder_documentos/... por AddFoldersDocumentos
    /// (conservando Ids), asi que aqui no se pierde informacion de negocio. Aplicar en prod SOLO despues de
    /// validar la copia (tarjetas y explorador de Folders con los expedientes migrados). El Down recrea el
    /// esquema vacio (lo genera EF); el rollback real con datos = backup previo al deploy.
    /// </summary>
    public partial class DropExpedientesV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expediente_campos");

            migrationBuilder.DropTable(
                name: "expediente_tipologia_versiones");

            migrationBuilder.DropTable(
                name: "subserie_campos");

            migrationBuilder.DropTable(
                name: "subserie_tipologias");

            migrationBuilder.DropTable(
                name: "expediente_tipologias");

            migrationBuilder.DropTable(
                name: "subseries_documentales");

            migrationBuilder.DropTable(
                name: "expedientes");

            migrationBuilder.DropTable(
                name: "series_documentales");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expedientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    serie = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    subserie = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expedientes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "series_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series_documentales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expediente_campos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expediente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expediente_campos", x => x.id);
                    table.ForeignKey(
                        name: "FK_expediente_campos_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expediente_tipologias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expediente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archivo_mime = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    archivo_nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    archivo_tamano = table.Column<long>(type: "bigint", nullable: false),
                    archivo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    meta_json = table.Column<string>(type: "text", nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    numero_versiones = table.Column<int>(type: "integer", nullable: false),
                    obligatoria = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expediente_tipologias", x => x.id);
                    table.ForeignKey(
                        name: "FK_expediente_tipologias_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subseries_documentales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    serie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subseries_documentales", x => x.id);
                    table.ForeignKey(
                        name: "FK_subseries_documentales_series_documentales_serie_id",
                        column: x => x.serie_id,
                        principalTable: "series_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expediente_tipologia_versiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expediente_tipologia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    archivo_mime = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    archivo_nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    archivo_tamano = table.Column<long>(type: "bigint", nullable: false),
                    archivo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    notas_cambio = table.Column<string>(type: "text", nullable: true),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expediente_tipologia_versiones", x => x.id);
                    table.ForeignKey(
                        name: "FK_expediente_tipologia_versiones_expediente_tipologias_expedi~",
                        column: x => x.expediente_tipologia_id,
                        principalTable: "expediente_tipologias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subserie_campos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subserie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subserie_campos", x => x.id);
                    table.ForeignKey(
                        name: "FK_subserie_campos_subseries_documentales_subserie_id",
                        column: x => x.subserie_id,
                        principalTable: "subseries_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subserie_tipologias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subserie_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subserie_tipologias", x => x.id);
                    table.ForeignKey(
                        name: "FK_subserie_tipologias_subseries_documentales_subserie_id",
                        column: x => x.subserie_id,
                        principalTable: "subseries_documentales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expediente_campos_expediente_id",
                table: "expediente_campos",
                column: "expediente_id");

            migrationBuilder.CreateIndex(
                name: "IX_expediente_campos_tenant_id_expediente_id_orden",
                table: "expediente_campos",
                columns: new[] { "tenant_id", "expediente_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "IX_expediente_tipologia_versiones_expediente_tipologia_id_nume~",
                table: "expediente_tipologia_versiones",
                columns: new[] { "expediente_tipologia_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expediente_tipologia_versiones_tenant_id",
                table: "expediente_tipologia_versiones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_expediente_tipologias_expediente_id",
                table: "expediente_tipologias",
                column: "expediente_id");

            migrationBuilder.CreateIndex(
                name: "IX_expediente_tipologias_tenant_id_expediente_id_orden",
                table: "expediente_tipologias",
                columns: new[] { "tenant_id", "expediente_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "IX_expedientes_tenant_id_codigo",
                table: "expedientes",
                columns: new[] { "tenant_id", "codigo" });

            migrationBuilder.CreateIndex(
                name: "IX_series_documentales_tenant_id_orden",
                table: "series_documentales",
                columns: new[] { "tenant_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "IX_subserie_campos_subserie_id",
                table: "subserie_campos",
                column: "subserie_id");

            migrationBuilder.CreateIndex(
                name: "IX_subserie_campos_tenant_id_subserie_id_orden",
                table: "subserie_campos",
                columns: new[] { "tenant_id", "subserie_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "IX_subserie_tipologias_subserie_id",
                table: "subserie_tipologias",
                column: "subserie_id");

            migrationBuilder.CreateIndex(
                name: "IX_subserie_tipologias_tenant_id_subserie_id_orden",
                table: "subserie_tipologias",
                columns: new[] { "tenant_id", "subserie_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "IX_subseries_documentales_serie_id",
                table: "subseries_documentales",
                column: "serie_id");

            migrationBuilder.CreateIndex(
                name: "IX_subseries_documentales_tenant_id_serie_id_orden",
                table: "subseries_documentales",
                columns: new[] { "tenant_id", "serie_id", "orden" });
        }
    }
}
