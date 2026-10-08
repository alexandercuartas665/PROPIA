using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DirectorioEtiquetasListaCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Directorio etiquetas pasa de FK al catalogo rico (etiquetas_catalogo) a un VALOR de texto
            // (lista de catalogo global directorio.etiqueta + custom por copropiedad).
            // ORDEN IMPORTANTE: (a) agregar columna valor, (b) BACKFILL desde el nombre de la etiqueta
            // ligada ANTES de soltar la FK/columna, (c) soltar FK + indices + columna etiqueta_id.

            // (a) Nueva columna de texto.
            migrationBuilder.AddColumn<string>(
                name: "valor",
                table: "directorio_etiquetas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // (b) Backfill: valor = nombre de la etiqueta del catalogo a la que apuntaba la asignacion.
            // Se hace ANTES de soltar la FK/columna etiqueta_id para no perder el dato.
            migrationBuilder.Sql(
                "UPDATE directorio_etiquetas de SET valor = ec.nombre " +
                "FROM etiquetas_catalogo ec WHERE de.etiqueta_id = ec.id;");

            // (c) Soltar la FK, los indices sobre etiqueta_id y la columna.
            migrationBuilder.DropForeignKey(
                name: "FK_directorio_etiquetas_etiquetas_catalogo_etiqueta_id",
                table: "directorio_etiquetas");

            migrationBuilder.DropIndex(
                name: "IX_directorio_etiquetas_etiqueta_id",
                table: "directorio_etiquetas");

            migrationBuilder.DropIndex(
                name: "IX_directorio_etiquetas_vinculo_id_etiqueta_id",
                table: "directorio_etiquetas");

            migrationBuilder.DropColumn(
                name: "etiqueta_id",
                table: "directorio_etiquetas");

            // Nuevo indice unico por (vinculo, valor).
            migrationBuilder.CreateIndex(
                name: "IX_directorio_etiquetas_vinculo_id_valor",
                table: "directorio_etiquetas",
                columns: new[] { "vinculo_id", "valor" },
                unique: true);

            // NOTA: la tabla etiquetas_catalogo (catalogo rico con color/icono/grupo/aplica) se CONSERVA
            // intacta a proposito; ya no se usa desde el Directorio. Su limpieza (drop) es tarea aparte.
            // La semilla de la lista directorio.etiqueta la aplica el seeder de arranque
            // (CatalogoListasSeeder), no esta migracion.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_directorio_etiquetas_vinculo_id_valor",
                table: "directorio_etiquetas");

            migrationBuilder.DropColumn(
                name: "valor",
                table: "directorio_etiquetas");

            migrationBuilder.AddColumn<Guid>(
                name: "etiqueta_id",
                table: "directorio_etiquetas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_directorio_etiquetas_etiqueta_id",
                table: "directorio_etiquetas",
                column: "etiqueta_id");

            migrationBuilder.CreateIndex(
                name: "IX_directorio_etiquetas_vinculo_id_etiqueta_id",
                table: "directorio_etiquetas",
                columns: new[] { "vinculo_id", "etiqueta_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_directorio_etiquetas_etiquetas_catalogo_etiqueta_id",
                table: "directorio_etiquetas",
                column: "etiqueta_id",
                principalTable: "etiquetas_catalogo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
