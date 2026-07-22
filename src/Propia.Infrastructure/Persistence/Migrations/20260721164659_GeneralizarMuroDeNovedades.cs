using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// El muro de novedades deja de ser exclusivo de zonas comunes y pasa a colgar de
    /// cualquier entidad via (entidad_tipo, entidad_id).
    ///
    /// ESCRITA A MANO A PROPOSITO. El scaffolding de EF proponia DROP + CREATE de las tres
    /// tablas, lo que (a) borraba todas las novedades ya publicadas y (b) perdia la politica
    /// RLS tenant_isolation que AddRlsToRemainingTenantTables les activo, dejando el muro sin
    /// aislamiento entre copropiedades. Renombrar conserva ambas cosas: en Postgres las
    /// politicas y los indices siguen a la tabla.
    /// </summary>
    public partial class GeneralizarMuroDeNovedades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----- Tablas -----
            migrationBuilder.RenameTable(name: "zona_novedades", newName: "novedades");
            migrationBuilder.RenameTable(name: "zona_novedad_comentarios", newName: "novedad_comentarios");
            migrationBuilder.RenameTable(name: "zona_novedad_likes", newName: "novedad_likes");

            // ----- Claves primarias (el nombre del constraint no se renombra solo) -----
            migrationBuilder.Sql("ALTER TABLE novedades RENAME CONSTRAINT \"PK_zona_novedades\" TO \"PK_novedades\";");
            migrationBuilder.Sql("ALTER TABLE novedad_comentarios RENAME CONSTRAINT \"PK_zona_novedad_comentarios\" TO \"PK_novedad_comentarios\";");
            migrationBuilder.Sql("ALTER TABLE novedad_likes RENAME CONSTRAINT \"PK_zona_novedad_likes\" TO \"PK_novedad_likes\";");

            // ----- Columnas -----
            migrationBuilder.RenameColumn(name: "zona_comun_id", table: "novedades", newName: "entidad_id");
            migrationBuilder.RenameColumn(name: "zona_novedad_id", table: "novedad_comentarios", newName: "novedad_id");
            migrationBuilder.RenameColumn(name: "zona_novedad_id", table: "novedad_likes", newName: "novedad_id");

            // Todo lo que existe hoy nacio en una zona comun => TipoEntidadNovedad.ZonaComun (1).
            migrationBuilder.AddColumn<int>(
                name: "entidad_tipo",
                table: "novedades",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // ----- Indices del muro (antes no habia ninguno; el muro se leia por zona) -----
            migrationBuilder.CreateIndex(
                name: "IX_novedades_entidad_tipo_entidad_id",
                table: "novedades",
                columns: new[] { "entidad_tipo", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "IX_novedad_comentarios_novedad_id",
                table: "novedad_comentarios",
                column: "novedad_id");

            migrationBuilder.CreateIndex(
                name: "IX_novedad_likes_novedad_id_persona_id",
                table: "novedad_likes",
                columns: new[] { "novedad_id", "persona_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_novedades_entidad_tipo_entidad_id", table: "novedades");
            migrationBuilder.DropIndex(name: "IX_novedad_comentarios_novedad_id", table: "novedad_comentarios");
            migrationBuilder.DropIndex(name: "IX_novedad_likes_novedad_id_persona_id", table: "novedad_likes");

            // Se conservan solo las novedades de zonas comunes: las de otras entidades no
            // caben en el modelo viejo, que solo tenia zona_comun_id.
            migrationBuilder.Sql(@"
DELETE FROM novedad_likes WHERE novedad_id IN (SELECT id FROM novedades WHERE entidad_tipo <> 1);
DELETE FROM novedad_comentarios WHERE novedad_id IN (SELECT id FROM novedades WHERE entidad_tipo <> 1);
DELETE FROM novedades WHERE entidad_tipo <> 1;");

            migrationBuilder.DropColumn(name: "entidad_tipo", table: "novedades");

            migrationBuilder.RenameColumn(name: "entidad_id", table: "novedades", newName: "zona_comun_id");
            migrationBuilder.RenameColumn(name: "novedad_id", table: "novedad_comentarios", newName: "zona_novedad_id");
            migrationBuilder.RenameColumn(name: "novedad_id", table: "novedad_likes", newName: "zona_novedad_id");

            migrationBuilder.Sql("ALTER TABLE novedades RENAME CONSTRAINT \"PK_novedades\" TO \"PK_zona_novedades\";");
            migrationBuilder.Sql("ALTER TABLE novedad_comentarios RENAME CONSTRAINT \"PK_novedad_comentarios\" TO \"PK_zona_novedad_comentarios\";");
            migrationBuilder.Sql("ALTER TABLE novedad_likes RENAME CONSTRAINT \"PK_novedad_likes\" TO \"PK_zona_novedad_likes\";");

            migrationBuilder.RenameTable(name: "novedades", newName: "zona_novedades");
            migrationBuilder.RenameTable(name: "novedad_comentarios", newName: "zona_novedad_comentarios");
            migrationBuilder.RenameTable(name: "novedad_likes", newName: "zona_novedad_likes");
        }
    }
}
